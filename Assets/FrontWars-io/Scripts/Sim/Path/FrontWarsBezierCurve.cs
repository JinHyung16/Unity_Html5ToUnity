using System;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 3차 베지어 곡선을 일정 간격 점으로 미리 뽑아 두고, 이동 거리만큼 점 번호를 앞으로 민다
    /// `[소스] Cq3gr8XO.js:Td=class{constructor(e,t,n,r){this.p0=e` · `Ed=class extends Td{`.
    ///
    /// <para>
    /// 점 뽑기: t 를 0.002 씩 올리며 직전 점과의 거리를 누적하고, 누적이 간격 이상이면 점을 넣고 누적을 0 으로.
    /// 마지막에 t=1 점이 마지막 점과 다르면 넣는다. 모든 계산은 64비트 소수이며 곱셈 · 덧셈 순서도 원본 그대로다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsBezierCurve
    {
        /// <summary>곡선 매개변수 한 걸음 `[소스] this.computeAllPoints(i,.002)`.</summary>
        public const double SampleStep = 0.002;

        private readonly double _p0X, _p0Y, _p1X, _p1Y, _p2X, _p2Y, _p3X, _p3Y;
        private readonly List<double> _xs = new List<double>();
        private readonly List<double> _ys = new List<double>();

        /// <summary>점 0 부터 i 까지의 꺾은선 길이 — 원본은 매번 처음부터 더하지만 더하는 순서가 같아 값도 같다.</summary>
        private readonly List<double> _distanceUpTo = new List<double>();

        private double _totalDistance;
        private int _currentIndex;

        public FrontWarsBezierCurve(double p0X, double p0Y, double p1X, double p1Y, double p2X, double p2Y, double p3X, double p3Y, double pointSpacing)
        {
            _p0X = p0X;
            _p0Y = p0Y;
            _p1X = p1X;
            _p1Y = p1Y;
            _p2X = p2X;
            _p2Y = p2Y;
            _p3X = p3X;
            _p3Y = p3Y;
            ComputeAllPoints(pointSpacing, SampleStep);
        }

        public int PointCount
        {
            get { return _xs.Count; }
        }

        public int CurrentIndex
        {
            get { return _currentIndex; }
        }

        public double PointX(int index)
        {
            return _xs[index];
        }

        public double PointY(int index)
        {
            return _ys[index];
        }

        private void PointAt(double t, out double x, out double y)
        {
            double u = 1 - t;
            double u2 = u * u;
            double u3 = u2 * u;
            double t2 = t * t;
            double t3 = t2 * t;

            x = u3 * _p0X + 3 * u2 * t * _p1X + 3 * u * t2 * _p2X + t3 * _p3X;
            y = u3 * _p0Y + 3 * u2 * t * _p1Y + 3 * u * t2 * _p2Y + t3 * _p3Y;
        }

        private void ComputeAllPoints(double spacing, double step)
        {
            _xs.Clear();
            _ys.Clear();
            _totalDistance = 0;
            _currentIndex = 0;

            double t = 0;
            PointAt(t, out double prevX, out double prevY);
            _xs.Add(prevX);
            _ys.Add(prevY);

            double accumulated = 0;

            while (t < 1)
            {
                t = Math.Min(t + step, 1);
                PointAt(t, out double x, out double y);

                double dx = x - prevX, dy = y - prevY;
                accumulated += Math.Sqrt(dx * dx + dy * dy);

                if (accumulated >= spacing)
                {
                    _xs.Add(x);
                    _ys.Add(y);
                    accumulated = 0;
                }

                prevX = x;
                prevY = y;
            }

            PointAt(1, out double endX, out double endY);
            int last = _xs.Count - 1;
            if (_xs.Count == 0 || endX != _xs[last] || endY != _ys[last])
            {
                _xs.Add(endX);
                _ys.Add(endY);
            }

            _distanceUpTo.Clear();
            _distanceUpTo.Add(0);
            for (int i = 1; i < _xs.Count; i++)
            {
                double dx = _xs[i] - _xs[i - 1], dy = _ys[i] - _ys[i - 1];
                _distanceUpTo.Add(_distanceUpTo[i - 1] + Math.Sqrt(dx * dx + dy * dy));
            }
        }

        /// <summary>
        /// 이동 거리를 더하고 점 번호를 민다. 마지막 점에 닿았으면 -1, 아니면 현재 점 번호 `[소스] increment(e){`.
        /// </summary>
        public int Increment(double distance)
        {
            _totalDistance += distance;

            while (_currentIndex < _xs.Count - 1 && _distanceUpTo[_currentIndex + 1] < _totalDistance)
                _currentIndex++;

            return _currentIndex >= _xs.Count - 1 ? -1 : _currentIndex;
        }
    }
}
