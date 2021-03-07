using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace zzmaps
{
    class BranchBlock<T> : IPropagatorBlock<T, T>
    {
        private readonly ExecutionDataflowBlockOptions options;
        private readonly TransformManyBlock<T, (int, T)> multiplyBlock;
        private readonly Dictionary<ITargetBlock<T>, ITargetBlock<(int, T)>> targets = new Dictionary<ITargetBlock<T>, ITargetBlock<(int, T)>>();

        private IPropagatorBlock<T, (int, T)> multiplyBlockInternal => multiplyBlock;

        public BranchBlock(ExecutionDataflowBlockOptions options)
        {
            this.options = options;
            multiplyBlock = new TransformManyBlock<T, (int, T)>(value => Enumerable
                .Range(0, targets.Count)
                .Select(i => (i, value)),
                options);
        }

        public Task Completion => multiplyBlock.Completion;
        public void Complete() => multiplyBlock.Complete();

        public T? ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target, out bool messageConsumed) =>
            multiplyBlockInternal.ConsumeMessage(messageHeader, targets[target], out messageConsumed).Item2;

        public void Fault(Exception exception) => multiplyBlockInternal.Fault(exception);

        public IDisposable LinkTo(ITargetBlock<T> target, DataflowLinkOptions linkOptions)
        {
            var extract = new TransformBlock<(int, T), T>(t => t.Item2, options);
            var newTargetI = targets.Count;
            multiplyBlock.LinkTo(extract, linkOptions, t => t.Item1 == newTargetI);
            targets.Add(target, extract);
            return extract.LinkTo(target, linkOptions);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, T messageValue, ISourceBlock<T>? source, bool consumeToAccept) =>
            multiplyBlockInternal.OfferMessage(messageHeader, messageValue, source, consumeToAccept);

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<T> target) =>
            multiplyBlockInternal.ReleaseReservation(messageHeader, targets[target]);

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target) =>
            multiplyBlockInternal.ReserveMessage(messageHeader, targets[target]);
    }
}
