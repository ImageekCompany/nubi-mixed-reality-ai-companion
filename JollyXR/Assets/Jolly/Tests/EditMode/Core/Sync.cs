using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core
{
    /// <summary>Runs work driven entirely by fakes: it must already be complete, and its exception is rethrown.</summary>
    static class Sync
    {
        public static void Run(UniTask task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending), "task did not complete synchronously");
            task.GetAwaiter().GetResult();
        }

        public static T Run<T>(UniTask<T> task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending), "task did not complete synchronously");
            return task.GetAwaiter().GetResult();
        }
    }
}
