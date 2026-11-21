using System;

namespace IssaPlugin.Compatibility
{
    // Server time only: input updates never grant shots or reset the spin-up.
    internal sealed class AutomaticFireClock
    {
        private double _next;
        internal AutomaticFireClock(double now, double spinUp) => _next = now + Math.Max(0, spinUp);
        internal int TakeDue(double now, double interval, int ammunition)
        {
            if (!double.IsFinite(now) || !double.IsFinite(interval) || interval < 0.01 || ammunition <= 0 || now + 1e-9 < _next)
                return 0;
            double requested = Math.Floor((now - _next + 1e-9) / interval) + 1;
            int due = Math.Min((int)Math.Min(4, requested), ammunition);
            _next += due * interval;
            // A stall never accrues an unbounded burst of retroactive shots.
            if (requested > 4) _next = now + interval;
            return due;
        }
    }
}
