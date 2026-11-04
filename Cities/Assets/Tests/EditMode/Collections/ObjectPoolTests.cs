using System;
using System.Collections.Generic;
using Cities.Core.Collections;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Collections
{
    public class ObjectPoolTests
    {
        [Test]
        public void ReturnedObjectsAreReusedAndReset()
        {
            var pool = new ObjectPool<List<int>>(() => new List<int>(), list => list.Clear());
            var first = pool.Get();
            first.Add(1);
            pool.Return(first);

            var second = pool.Get();

            Assert.That(second, Is.SameAs(first));
            Assert.That(second, Is.Empty);
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
        }

        [Test]
        public void StableCreatedCountOverManyRentals()
        {
            var pool = new ObjectPool<List<int>>(() => new List<int>(), list => list.Clear());

            for (var i = 0; i < 100; i++)
            {
                using (var lease = pool.Rent())
                {
                    lease.Value.Add(i);
                }
            }

            Assert.That(pool.CreatedCount, Is.EqualTo(1));
            Assert.That(pool.InUse, Is.EqualTo(0));
            Assert.That(pool.FreeCount, Is.EqualTo(1));
        }

        [Test]
        public void PoolKeepsAtMostMaxRetained()
        {
            var pool = new ObjectPool<object>(() => new object(), maxRetained: 2);
            var items = new[] { pool.Get(), pool.Get(), pool.Get() };

            foreach (var item in items)
            {
                pool.Return(item);
            }

            Assert.That(pool.FreeCount, Is.EqualTo(2));
            pool.Clear();
            Assert.That(pool.FreeCount, Is.EqualTo(0));
        }

        [Test]
        public void InvalidUseIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new ObjectPool<object>(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectPool<object>(() => new object(), null, -1));
            Assert.Throws<ArgumentNullException>(() => new ObjectPool<object>(() => new object()).Return(null));
        }

        [Test]
        public void ListPoolHandsOutCleanLists()
        {
            var list = ListPool<int>.Get();
            list.Add(5);
            ListPool<int>.Return(list);

            Assert.That(ListPool<int>.Get(), Is.Empty);
            Assert.That(ListPool<int>.Instance.MaxRetained, Is.GreaterThan(0));
        }
    }
}
