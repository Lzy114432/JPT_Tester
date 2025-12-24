using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EwanAxis.Core.Interfaces
{
    public static class AxisAsyncExtensions
    {
        public static Task<bool> WaitIdleAsync(this IAxis axis, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return WaitIdleAsync(axis, timeout, pollInterval: TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        public static async Task<bool> WaitIdleAsync(this IAxis axis, TimeSpan timeout, TimeSpan pollInterval, CancellationToken cancellationToken = default)
        {
            if (axis == null) throw new ArgumentNullException(nameof(axis));
            if (pollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollInterval));

            DateTime deadline = timeout == Timeout.InfiniteTimeSpan
                ? DateTime.MaxValue
                : DateTime.UtcNow + timeout;

            while (axis.IsBusy)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow >= deadline) return false;
                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        public static async Task<bool> AbsMoveAsync(this IAxis axis, double position, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (axis == null) throw new ArgumentNullException(nameof(axis));

            if (!axis.AbsMove(position)) return false;
            return await axis.WaitIdleAsync(timeout, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<bool> RelMoveAsync(this IAxis axis, double distance, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (axis == null) throw new ArgumentNullException(nameof(axis));

            if (!axis.RelMove(distance)) return false;
            return await axis.WaitIdleAsync(timeout, cancellationToken).ConfigureAwait(false);
        }

        public static Task<bool> HomeAsync(this IAxis axis, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return HomeAsync(axis, timeout, pollInterval: TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        public static async Task<bool> HomeAsync(this IAxis axis, TimeSpan timeout, TimeSpan pollInterval, CancellationToken cancellationToken = default)
        {
            if (axis == null) throw new ArgumentNullException(nameof(axis));
            if (pollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollInterval));

            axis.Home();

            DateTime deadline = timeout == Timeout.InfiniteTimeSpan
                ? DateTime.MaxValue
                : DateTime.UtcNow + timeout;

            while (!axis.HomeIsDown())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow >= deadline) return false;
                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        public static Task<bool> WaitAllIdleAsync(this IEnumerable<IAxis> axes, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return WaitAllIdleAsync(axes, timeout, pollInterval: TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        public static async Task<bool> WaitAllIdleAsync(this IEnumerable<IAxis> axes, TimeSpan timeout, TimeSpan pollInterval, CancellationToken cancellationToken = default)
        {
            if (axes == null) throw new ArgumentNullException(nameof(axes));
            if (pollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollInterval));

            var axisArray = axes as IAxis[] ?? axes.ToArray();

            DateTime deadline = timeout == Timeout.InfiniteTimeSpan
                ? DateTime.MaxValue
                : DateTime.UtcNow + timeout;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool anyBusy = false;
                foreach (var axis in axisArray)
                {
                    if (axis.IsBusy)
                    {
                        anyBusy = true;
                        break;
                    }
                }

                if (!anyBusy) return true;
                if (DateTime.UtcNow >= deadline) return false;

                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

