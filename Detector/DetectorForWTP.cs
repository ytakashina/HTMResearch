﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Clustering;
using MoreLinq;
using PipExtensions;
using static Clustering.Clustering;
using static MatViz.MatViz;
using static PipExtensions.PipExtensions;

namespace Detector
{
    public class DetectorForWTP
    {
        private List<List<int>> _series1;
        private List<List<int>> _series2;
        private List<List<int>> _series3;
        private double[,] _relationships;
        // 各階層におけるクラスタ数
        private const int N1 = 32;
        private const int N2 = 8;
        private const int N3 = 4;
        // 各階層におけるノード数
        private const int M1 = 38;
        private const int M2 = 6;
        private const int M3 = 1;
        // Spatial Pooler
        private readonly List<List<int>>[] _spatialPoolerList1 = new List<List<int>>[M1];
        private readonly List<List<int>>[] _spatialPoolerList2 = new List<List<int>>[M2];
        private readonly List<List<int>>[] _spatialPoolerList3 = new List<List<int>>[M3];
        // Level 1
        private readonly double[][,] _transitions1 = new double[M1][,];
        private readonly double[][,] _probabilities1 = new double[M1][,];
        private readonly double[][,] _distances1 = new double[M1][,];
        // Level 2
        private readonly double[][,] _transitions2 = new double[M2][,];
        private readonly double[][,] _probabilities2 = new double[M2][,];
        private readonly double[][,] _distances2 = new double[M2][,];
        // Level 3
        private readonly double[][,] _transitions3 = Enumerable.Repeat(new double[N3, N3], M3).ToArray();
        private readonly double[][,] _probabilities3 = Enumerable.Repeat(new double[N3, N3], M3).ToArray();
        private readonly double[][,] _distances3 = Enumerable.Repeat(new double[N3, N3], M3).ToArray();
        // 帰属度行列1
        private readonly int[][,] _membership1TP = Enumerable.Repeat(new int[N1, N2], M1).ToArray();
        private readonly int[][,] _membership2TP = Enumerable.Repeat(new int[N1, N2], M2).ToArray();
        // 帰属度行列2
        private readonly int[][,] _membership12SP = Enumerable.Repeat(new int[M1, M2], N2).ToArray();
        private readonly int[][,] _membership23SP = Enumerable.Repeat(new int[M2, M3], N3).ToArray();

        public void Initialize(List<List<double>> rawSeries)
        {
            _series1 = _discretizeSeries(rawSeries);
            _series2 = new List<List<int>>();
            _series3 = new List<List<int>>();
            for (var m1 = 0; m1 < M1; m1++)
            {
                _spatialPoolerList1[m1] = new List<List<int>>();
//                _series1.Add(new List<int>());
                _transitions1[m1] = new double[N1, N1];
                _probabilities1[m1] = new double[N1, N1];
                _distances1[m1] = new double[N1, N1];
            }
            for (var m2 = 0; m2 < M2; m2++)
            {
                _spatialPoolerList2[m2] = new List<List<int>>();
                _series2.Add(new List<int>());
//                _transitions2[m2] = new double[N1, N1];
//                _probabilities2[m2] = new double[N1, N1];
//                _distances2[m2] = new double[N1, N1];
            }
            for (var m3 = 0; m3 < M3; m3++)
            {
                _spatialPoolerList2[m3] = new List<List<int>>();
                _series3.Add(new List<int>());
            }
            _relationships = MutualInformationMatrix(_series1);
        }

        public void Learn()
        {
            for (var i = 0; i < M1; i++)
            {
                // 遷移のカウント
                for (var j = 0; j < _series1[i].Count - 1; j++)
                {
                    _transitions1[i][_series1[i][j], _series1[i][j + 1]] += 1;
                }
                // Level 1
                _probabilities1[i] = _transitions1[i].NormalizeToRaw();
                for (var j = 0; j < N1; j++)
                {
                    for (var k = 0; k < N1; k++)
                    {
                        _distances1[i][j, k] = 1 - (_probabilities1[i][j, k] + _probabilities1[i][k, j])/2;
                    }
                }
                // Level 1 の Temporal Pooling
                var cluster1 = AggregativeHierarchicalClustering(Enumerable.Range(0, N1).ToArray(), (j, k) => _distances1[i][j, k], Metrics.GroupAverage);
                var cluster1Members = cluster1.Extract(N2).Select(c => c.GetMembers().Select(s => s.Value)).ToArray();
                for (var k = 0; k < N2; k++)
                {
                    for (var j = 0; j < N1; j++)
                    {
                        _membership1TP[i][j, k] = cluster1Members[k].Contains(j) ? 1 : 0;
                    }
                }
            }
            // Level 1-2 間の Spatial Pooling
            for (var i = 0; i < _series1.First().Count; i++)
            {
                for (var m2 = 0; m2 < M2; m2++)
                {
                    _series2[m2].Add(-1);
                    var list = new List<int>();
                    for (var m1 = m2; m1 < M1; m1 += M2)
                    {
                        var pattern = Enumerable.Range(0, N1).Select(j => j == _series1[m1][i] ? 1 : 0).ToArray();
                        var group = _membership1TP[m1].T().Mul(pattern);
                        list.AddRange(group);
                    }
                    if (_spatialPoolerList2[m2].All(pattern => !pattern.SequenceEqual(list))) _spatialPoolerList2[m2].Add(list);
                    for (var j = 0; j < _spatialPoolerList2[m2].Count; j++)
                    {
                        if (_spatialPoolerList2[m2][j].SequenceEqual(list))
                        {
                            _series2[m2][i] = j;
                        }
                    }
                }
            }
            for (var m2 = 0; m2 < M2; m2++)
            {
                var count = _spatialPoolerList2[m2].Count;
                _transitions2[m2] = new double[count, count];
                _probabilities2[m2] = new double[count, count];
                _distances2[m2] = new double[count, count];
            }
            for (var i = 0; i < M2; i++)
            {
                // 遷移のカウント
                for (var j = 0; j < _series2[i].Count - 1; j++)
                {
                    _transitions2[i][_series2[i][j], _series2[i][j + 1]] += 1;
                }
                // Level 2 
                _probabilities2[i] = _transitions2[i].NormalizeToRaw();
                for (var j = 0; j < N1; j++)
                {
                    for (var k = 0; k < N1; k++)
                    {
                        _distances2[i][j, k] = 1 - (_probabilities2[i][j, k] + _probabilities2[i][k, j])/2;
                    }
                }
                // Level 2 の Temporal Pooling
                var cluster2 = AggregativeHierarchicalClustering(Enumerable.Range(0, _spatialPoolerList2[i].Count).ToArray(), (j, k) => _distances2[i][j, k], Metrics.GroupAverage);
                var cluster2Members = cluster2.Extract(N2).Select(c => c.GetMembers().Select(s => s.Value)).ToArray();
                for (var k = 0; k < N2; k++)
                {
                    for (var j = 0; j < N1; j++)
                    {
                        _membership2TP[i][j, k] = cluster2Members[k].Contains(j) ? 1 : 0;
                    }
                }
            }
        }

        public void ClusterSeries()
        {
            var cluster = AggregativeHierarchicalClustering(Enumerable.Range(0, _relationships.GetLength(0)).ToArray(), (i, j) => _relationships[i, j], Metrics.GroupAverage);
            var cluster1Members = cluster.Extract(8).Select(c => c.GetMembers().Select(s => s.Value)).ToArray();
            var order = cluster1Members.SelectMany(i => i).ToArray();
            var memberships = new double[38, 8];
            for (var k = 0; k < 8; k++)
            {
                for (var j = 0; j < 38; j++)
                {
                    memberships[j, k] = cluster1Members[k].Contains(j) ? 1.0 : 1e-6;
                }
            }
            SaveMatrixImage(memberships, "membership");
            _relationships = _relationships.OrderRaws(order);
            _relationships = _relationships.OrderCols(order);
            SaveMatrixImage(_relationships, "sorted");
            cluster.Print();
        }

        private static List<List<int>> _discretizeSeries(List<List<double>> rawSeries)
        {
            var list = new List<List<int>>();
            var discretizedValues = rawSeries.Select(series => Sampling.CalcSamplePoints(series, N1, true).ToList()).ToList();
            for (var i = 0; i < rawSeries.Count; i++)
            {
                var discretizedSeries = new List<int>();
                foreach (var value in rawSeries[i])
                {
                    var discretizedValue = double.IsNaN(value) ? value : discretizedValues[i].Where(v => !double.IsNaN(v)).MinBy(v => Math.Abs(v - value));
                    var discretizedValueIndex = discretizedValues[i].IndexOf(discretizedValue);
                    discretizedSeries.Add(discretizedValueIndex);
                }
                list.Add(discretizedSeries);
            }
            return list;
        }

        public void SaveResultImages(string path = ".")
        {
            path += @"\multimodal\";
            Directory.CreateDirectory(path);
            SaveMatrixImage(_relationships, path + "mutual_information_matrix");
            for (var i = 0; i < M1; i++)
            {
                SaveMatrixImage(_transitions1[i], path + "layer1_transitions" + i);
                SaveMatrixImage(_probabilities1[i], path + "layer1_probabilities" + i);
                SaveMatrixImage(_distances1[i], path + "layer1_distances_mean" + i, threshold: double.MaxValue, bgWhite: false);
            }
            for (var i = 0; i < M2; i++)
            {
//            SaveMatrixImage(_membership12SP[0], path + "layer12_membership");
                SaveMatrixImage(_probabilities2[i], path + "layer2_probabilities" + i);
                SaveMatrixImage(_distances2[i], path + "layer2_distances_mean" + i, threshold: double.MaxValue, bgWhite: false);
            }
        }
    }
}