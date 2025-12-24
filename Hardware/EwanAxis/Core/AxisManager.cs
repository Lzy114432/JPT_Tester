using System;
using System.Collections.Generic;
using System.Linq;

namespace EwanAxis.Core.Interfaces
{
    /// <summary>
    /// 多轴/多卡管理器：用于一个项目管理多张轴卡与多轴实例。
    /// </summary>
    public sealed class AxisManager : IDisposable
    {
        private readonly List<IAxisCard> _cards = new List<IAxisCard>();
        private bool _disposed;

        public IReadOnlyList<IAxisCard> Cards => _cards.AsReadOnly();

        public IEnumerable<IAxis> Axes => _cards.SelectMany(c => c.Axes);

        public AxisManager AddCard(IAxisCard card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            ThrowIfDisposed();

            if (!_cards.Contains(card))
            {
                _cards.Add(card);
            }

            return this;
        }

        public bool RemoveCard(IAxisCard card)
        {
            if (card == null) return false;
            ThrowIfDisposed();
            return _cards.Remove(card);
        }

        public IAxis? GetAxisByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            ThrowIfDisposed();

            foreach (var card in _cards)
            {
                var axis = card.GetAxisByName(name);
                if (axis != null) return axis;
            }

            return null;
        }

        public bool TryGetAxis(int cardIndex, int axisIndex, out IAxis? axis)
        {
            axis = null;
            ThrowIfDisposed();

            var card = _cards.FirstOrDefault(c => c.CardIndex == cardIndex);
            if (card == null) return false;

            try
            {
                axis = card[axisIndex];
                return axis != null;
            }
            catch
            {
                return false;
            }
        }

        public bool ConnectAll()
        {
            ThrowIfDisposed();
            bool ok = true;
            foreach (var card in _cards)
            {
                ok &= card.Connect();
            }
            return ok;
        }

        public bool DisconnectAll()
        {
            ThrowIfDisposed();
            bool ok = true;
            foreach (var card in _cards)
            {
                ok &= card.Disconnect();
            }
            return ok;
        }

        public void EmgStopAll()
        {
            ThrowIfDisposed();
            foreach (var card in _cards)
            {
                card.EmgStopAll();
            }
        }

        public void ServoOnAll(bool enable)
        {
            ThrowIfDisposed();
            foreach (var card in _cards)
            {
                card.ServoOnAll(enable);
            }
        }

        public void ClearAllErrors()
        {
            ThrowIfDisposed();
            foreach (var card in _cards)
            {
                card.ClearAllErrors();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var card in _cards)
            {
                try
                {
                    card.Dispose();
                }
                catch
                {
                    // ignored
                }
            }

            _cards.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AxisManager));
            }
        }
    }
}

