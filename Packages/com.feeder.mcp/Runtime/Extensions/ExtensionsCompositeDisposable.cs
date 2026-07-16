#nullable enable
using System.Threading;
using R3;

namespace Feeder.MCP.Runtime.Extensions
{
    public static class ExtensionsCompositeDisposable
    {
        public static CancellationToken ToCancellationToken(this CompositeDisposable disposables)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            disposables.Add(Disposable.Create(() => cancellationTokenSource.Cancel()));
            return cancellationTokenSource.Token;
        }
    }
}
