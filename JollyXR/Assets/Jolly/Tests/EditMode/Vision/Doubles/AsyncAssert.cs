using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Vision.Doubles
{
    static class AsyncAssert
    {
        public static async UniTask<T> Throws<T>(Func<UniTask> action) where T : Exception
        {
            try
            {
                await action();
            }
            catch (T expected)
            {
                return expected;
            }
            catch (Exception other)
            {
                Assert.Fail($"Expected {typeof(T).Name} but got {other.GetType().Name}: {other.Message}");
            }
            Assert.Fail($"Expected {typeof(T).Name} but nothing was thrown.");
            return null;
        }
    }
}
