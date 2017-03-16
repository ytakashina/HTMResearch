﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Detector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PipExtensions;

namespace Detector.Tests
{
    [TestClass()]
    public class NodeTests
    {
        private readonly Node _node;

        public NodeTests()
        {
            _node = new Node(new List<int> {3, 4, 5, 4, 3, 4, 5, 8, 0, 0}, 2);
            _node.Learn();
        }

        [TestMethod()]
        public void ForwardTest()
        {
            var inputs = new[]
            {
                new[] {1, 0, 0, 0, 0},
                new[] {0, 1, 0, 0, 0},
                new[] {0, 0, 1, 0, 0},
                new[] {0, 0, 0, 1, 0},
                new[] {0, 0, 0, 0, 1},
            };
            var answers = new[,] {{0, 1}, {0, 1}, {0, 1}, {1, 0}, {1, 0}};
            for (var i = 0; i < 5; i++)
            {
                var output = _node.Forward(inputs[i]);
                for (var j = 0; j < 2; j++)
                {
                    Assert.AreEqual(answers[i, j], output[j]);
                }
            }
        }

        [TestMethod()]
        public void ForwardTest1() {}

        [TestMethod()]
        public void BackwardTest() {}

        [TestMethod()]
        public void BackwardTest1() {}

        [TestMethod()]
        public void LearnTest()
        {
            var ans = new[,] {{0, 1}, {0, 1}, {0, 1}, {1, 0}, {1, 0}};
            for (var i = 0; i < 5; i++)
            {
                for (var j = 0; j < 2; j++)
                {
                    Assert.AreEqual(ans[i, j], _node.Membership[i, j]);
                }
            }
        }

        [TestMethod()]
        public void PredictTest() {}
    }
}