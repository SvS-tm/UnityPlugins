using System;
using System.Collections.Generic;
using System.Threading;

namespace UnlimitedRestockers;

public class IdPool(int capacity)
{
    private readonly object syncBase = new();
    private bool[] pool = new bool[capacity];

    public IdPoolManipulator Manipulate()
    {
        return new IdPoolManipulator(this);
    }

    public struct IdPoolManipulator : IDisposable
    {
        private IdPool parent;

        public IdPoolManipulator(IdPool parent)
        {
            Monitor.Enter(parent.syncBase);

            this.parent = parent;
        }

        public int Reserve()
        {
            var index = Array.IndexOf(parent.pool, false);

            if (index == -1)
            {
                index = parent.pool.Length;

                EnsureSize(index);
            }

            parent.pool[index] = true;

            return index + 1;
        }

        public readonly int PickToRelease()
        {
            var index = Array.LastIndexOf(parent.pool, true);

            return index == -1 ? index : index + 1;
        }

        public readonly IEnumerable<int> GetReservedIds()
        {
            for (int index = 0; index < parent.pool.Length; ++index)
            {
                if (parent.pool[index])
                    yield return index + 1;
            }
        }

        private void EnsureSize(int probeIndex)
        {
            if (probeIndex >= parent.pool.Length)
                Array.Resize(ref parent.pool, Math.Max(parent.pool.Length * 2, probeIndex + 1));
        }

        public bool Reserve(int id)
        {
            var index = id - 1;

            EnsureSize(index);

            var reserved = parent.pool[index];

            parent.pool[index] = true;

            return !reserved;
        }

        public readonly void Release(int id)
        {
            parent.pool[id - 1] = false;
        }

        public readonly void Dispose()
        {
            Monitor.Exit(parent.syncBase);
        }
    }
}
