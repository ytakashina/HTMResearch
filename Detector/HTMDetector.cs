﻿using System;
using System.Linq;
using PipExtensions;
using MatViz;
using MoreLinq;
using RakuChart;

namespace Detector
{
    public class HTMDetector
    {
        private double[] _samplePoints;
        private int[] _series;
        private int[] _predictedSeries;
        private int[] _predictedSeries2;
        private double[] _errorSeries;
        private const int N1 = 32;
        private const int N2 = 8;
        private const int N3 = 4;

        public void Initialize(double[] rawData)
        {
            _samplePoints = Sampling.CalcSamplePoints(rawData, N1);
            _series = new int[rawData.Length];
            _predictedSeries = new int[rawData.Length];
            _predictedSeries2 = new int[rawData.Length];
            _errorSeries = new double[rawData.Length];
            for (var i = 0; i < rawData.Length; i++)
            {
                var min = double.MaxValue;
                var mini = 0;
                for (var j = 0; j < _samplePoints.Length; j++)
                {
                    var d = Math.Abs(rawData[i] - _samplePoints[j]);
                    if (d < min)
                    {
                        min = d;
                        mini = j;
                    }
                }
                _series[i] = mini;
            }
        }

        public void Detect()
        {
            // Level 1
            var transitions1 = new double[N1, N1];
            var probabilities1 = new double[N1, N1];
            var distances1Mean = new double[N1, N1];
            var distances1Min = new double[N1, N1];
            // Level 2
            var transitions2 = new double[N2, N2];
            var probabilities2 = new double[N2, N2];
            var distances2Mean = new double[N2, N2];
            var distances2Min = new double[N2, N2];
            // Level 3
            var transitions3 = new double[N3, N3];
            var probabilities3 = new double[N3, N3];
            var distances3Mean = new double[N3, N3];
            var distances3Min = new double[N3, N3];
            // 帰属度行列
            var membership12 = new double[N1, N2];
            var membership23 = new double[N2, N3];

            // 状態
            var state1 = new double[N1];
            var state2 = Enumerable.Range(0, N2).Select(i => 1.0/N2).ToArray();

            for (var i = 0; i < _series.Length - 2; i++)
            {
                // Level 1
                transitions1[_series[i], _series[i + 1]] += 1;
                probabilities1 = transitions1.NormalizeToRaw();
                for (var j = 0; j < N1; j++)
                {
                    for (var k = 0; k < N1; k++)
                    {
                        distances1Mean[j, k] = 1 - (probabilities1[j, k] + probabilities1[k, j])/2;
                        distances1Min[j, k] = 1 - Math.Max(probabilities1[j, k], probabilities1[k, j]);
                    }
                }
                // Level 1 の Level 2 に対する帰属度
                var cluster1 = Clustering.AggregativeHierarchicalClustering(Enumerable.Range(0, N1).ToArray(), (j, k) => distances1Min[j, k], Metrics.GroupAverage);
                var cluster1Members = cluster1.Extract(N2).Select(c => c.GetMembers().Select(s => s.Value)).ToArray();
                for (var j = 0; j < N1; j++)
                {
                    for (var k = 0; k < N2; k++)
                    {
                        membership12[j, k] = cluster1Members[k].Contains(j) ? 1 : 1e-6;
                    }
                }
                // Level 2
                int from2 = -1, to2 = -1;
                for (var j = 0; j < N2; j++)
                {
                    if (Math.Abs(membership12[_series[i], j] - 1) < 1e-6) from2 = j;
                    if (Math.Abs(membership12[_series[i + 1], j] - 1) < 1e-6) to2 = j;
                }
                transitions2[from2, to2] += 1;
                probabilities2 = transitions2.NormalizeToRaw();
                for (var j = 0; j < N2; j++)
                {
                    for (var k = 0; k < N2; k++)
                    {
                        distances2Mean[j, k] = 1 - (probabilities2[j, k] + probabilities2[k, j])/2;
                        distances2Min[j, k] = 1 - Math.Max(probabilities2[j, k], probabilities2[k, j]);
                    }
                }
                // Level 2 の Level 3 に対する帰属度
                var cluster2 = Clustering.AggregativeHierarchicalClustering(Enumerable.Range(0, N2).ToArray(), (j, k) => distances2Min[j, k], Metrics.GroupAverage);
                var cluster2Members = cluster2.Extract(N3).Select(c => c.GetMembers().Select(s => s.Value)).ToArray();
                for (var j = 0; j < N2; j++)
                {
                    for (var k = 0; k < N3; k++)
                    {
                        membership23[j, k] = cluster2Members[k].Contains(j) ? 1 : 1e-6;
                    }
                }
                // Level 3
                int from3 = -1, to3 = -1;
                for (var j = 0; j < N3; j++)
                {
                    if (Math.Abs(membership23[from2, j] - 1) < 1e-6) from3 = j;
                    if (Math.Abs(membership23[to2, j] - 1) < 1e-6) to3 = j;
                }
                transitions3[from3, to3] += 1;
                probabilities3 = transitions3.NormalizeToRaw();
                for (var j = 0; j < N3; j++)
                {
                    for (var k = 0; k < N3; k++)
                    {
                        distances3Mean[j, k] = 1 - (probabilities3[j, k] + probabilities3[k, j])/2;
                        distances3Min[j, k] = 1 - Math.Max(probabilities3[j, k], probabilities3[k, j]);
                    }
                }

                for (var j = 0; j < N1; j++)
                {
                    state1[j] = _series[i + 1] == j ? 1 : 1e-6;
                }
                var message1 = membership12.T().Mul(state1);
                var message2 = probabilities2.Mul(state2);
                for (var j = 0; j < N2; j++) state2[j] = message1[j]*message2[j];
                var sum = state2.Sum();
                for (var j = 0; j < N2; j++) state2[j] /= sum;
                var prediction = membership12.Mul(probabilities2.Mul(state2)).ToList();
                _predictedSeries[i + 2] = prediction.IndexOf(prediction.Max());
                var m = probabilities1.Mul(state1).ToList();
                //_predictedSeries2[i + 2] = m.IndexOf(m.Max());
                _errorSeries[i + 2] = prediction[_series[i + 2]];
                //state2.ForEach(v => Console.Write(v.ToString("F4") + ", "));
                //Console.WriteLine();
            }
            ChartExtensions.CreateChart(_series.Select(i => _samplePoints[i]).ToArray(), _predictedSeries.Select(i => _samplePoints[i]).ToArray(), _predictedSeries2.Select(i => _samplePoints[i]).ToArray()).SaveImage("test");
            ChartExtensions.CreateChart(_errorSeries).SaveImage("test2");
            //_predictedSeries.ForEach(v => Console.Write(v + ", "));
            //var g1 = membership12.Mul(membership23.Mul(new double[] {1, 0, 0, 0}));
            //var g2 = membership12.Mul(membership23.Mul(new double[] {0, 1, 0, 0}));
            //var g3 = membership12.Mul(membership23.Mul(new double[] {0, 0, 1, 0}));
            //var g4 = membership12.Mul(membership23.Mul(new double[] {0, 0, 0, 1}));
            //g1.ForEach((p, i) => { if (Math.Abs(p - 1) < 1e-6) Console.Write(_samplePoints[i] + ", "); });
            //Console.WriteLine();
            //g2.ForEach((p, i) => { if (Math.Abs(p - 1) < 1e-6) Console.Write(_samplePoints[i] + ", "); });
            //Console.WriteLine();
            //g3.ForEach((p, i) => { if (Math.Abs(p - 1) < 1e-6) Console.Write(_samplePoints[i] + ", "); });
            //Console.WriteLine();
            //g4.ForEach((p, i) => { if (Math.Abs(p - 1) < 1e-6) Console.Write(_samplePoints[i] + ", "); });
            //Console.WriteLine();
            // 並べ替えて表示云々
            //var c1 = Clustering.AggregativeHierarchicalClusteringByName(Enumerable.Range(0, N1).ToArray(), (j, k) => distances1Mean[j, k], Clustering.AHCType.GroupAverage);
            //var cluster1Order = c1.Extract(N2).Select(c => c.GetMembers().Select(s => s.Value)).SelectMany(i => i).ToArray();
            //distances1Mean = distances1Mean.OrderRaws(cluster1Order);
            //distances1Mean = distances1Mean.OrderCols(cluster1Order);
            //distances1Min = distances1Min.OrderRaws(cluster1Order);
            //distances1Min = distances1Min.OrderCols(cluster1Order);
            //membership12 = membership12.OrderRaws(cluster1Order);
            //c1.Extract(N2).Select(c => c.GetMembers()).ForEach((singles, idx) => Console.WriteLine(idx + ": " + singles.Select(s => s.Value).Concatenate()));
            //cluster1Order.ForEach(x => Console.Write(x + ", "));
            MatViz.MatViz.SaveMatrixImage(transitions1, "layer1_transitions");
            MatViz.MatViz.SaveMatrixImage(probabilities1, "layer1_probabilities");
            MatViz.MatViz.SaveMatrixImage(distances1Mean, "layer1_distances_mean", threshold: double.MaxValue, bgWhite: false);
            MatViz.MatViz.SaveMatrixImage(distances1Min, "layer1_distances_min", threshold: double.MaxValue, bgWhite: false);
            MatViz.MatViz.SaveMatrixImage(membership12, "layer12_membership");
            MatViz.MatViz.SaveMatrixImage(transitions2, "layer2_transitions");
            MatViz.MatViz.SaveMatrixImage(probabilities2, "layer2_probabilities");
            MatViz.MatViz.SaveMatrixImage(distances2Mean, "layer2_distances_mean", threshold: double.MaxValue, bgWhite: false);
            MatViz.MatViz.SaveMatrixImage(distances2Min, "layer2_distances_min", threshold: double.MaxValue, bgWhite: false);
            MatViz.MatViz.SaveMatrixImage(membership23, "layer23_membership");
            MatViz.MatViz.SaveMatrixImage(transitions3, "layer3_transitions");
            MatViz.MatViz.SaveMatrixImage(probabilities3, "layer3_probabilities");
            MatViz.MatViz.SaveMatrixImage(distances3Mean, "layer3_distances_mean", threshold: 1, bgWhite: false);
            MatViz.MatViz.SaveMatrixImage(distances3Min, "layer3_distances_min", threshold: 1, bgWhite: false);
        }
    }
}