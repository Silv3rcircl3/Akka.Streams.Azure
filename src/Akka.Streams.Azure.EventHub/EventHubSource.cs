﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Streams.Stage;
using Microsoft.ServiceBus.Messaging;

namespace Akka.Streams.Azure.EventHub
{
    public class EventHubSource : GraphStageWithMaterializedValue<SourceShape<Tuple<PartitionContext, EventData>>, IEventProcessor>
    {
        #region Logic

        private sealed class ProcessContext
        {
            public ProcessContext(TaskCompletionSource<NotUsed> completion, PartitionContext context,
                IEnumerable<EventData> events)
            {
                Completion = completion;
                Context = context;
                Events = events;
            }

            public TaskCompletionSource<NotUsed> Completion { get; }

            public PartitionContext Context { get; }

            public IEnumerable<EventData> Events { get; }
        }

        private sealed class Logic : GraphStageLogic, IEventProcessor
        {
            private readonly EventHubSource _source;
            private Action<TaskCompletionSource<NotUsed>> _openCallback;
            private Action<TaskCompletionSource<NotUsed>> _closeCallback;
            private Action<ProcessContext> _processCallback;
            private TaskCompletionSource<NotUsed> _pendingCompletion;
            private Queue<EventData> _pendingEvents;
            private PartitionContext _currentContext;
            private int _partitionCount;

            public Logic(EventHubSource source) : base(source.Shape)
            {
                _source = source;
                SetHandler(source.Out, TryPush);
            }

            public override void PreStart()
            {
                _openCallback = GetAsyncCallback<TaskCompletionSource<NotUsed>>(OnOpen);
                _closeCallback = GetAsyncCallback<TaskCompletionSource<NotUsed>>(OnClose);
                _processCallback = GetAsyncCallback<ProcessContext>(OnProcessEvents);
            }

            public Task OpenAsync(PartitionContext context)
            {
                var completion = new TaskCompletionSource<NotUsed>();
                _openCallback(completion);
                return completion.Task;
            }

            private void OnOpen(TaskCompletionSource<NotUsed> completion)
            {
                // We need to count the partitions to close the stage only on the last close call,
                // otherwise further calls to the _closeCallback wouldn't be handled because they
                // are moved to DeadLetters and the close task is never completed
                _partitionCount++;
                completion.TrySetResult(NotUsed.Instance);
            }

            public Task CloseAsync(PartitionContext context, CloseReason reason)
            {
                var completion = new TaskCompletionSource<NotUsed>();
                _closeCallback(completion);

                return _source._createCheckpointOnClose
                    ? Task.WhenAll(completion.Task, context.CheckpointAsync())
                    : completion.Task;
            }

            private void OnClose(TaskCompletionSource<NotUsed> completion)
            {
                if(--_partitionCount == 0)
                    CompleteStage();
                completion.TrySetResult(NotUsed.Instance);
            }

            public Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
            {
                var completion = new TaskCompletionSource<NotUsed>();
                _processCallback(new ProcessContext(completion, context, messages));
                return completion.Task;
            }

            private void OnProcessEvents(ProcessContext context)
            {
                // ProcessEventsAsync is only called when the previous task is completed, 
                // therefore we can simply replace the previous values
                _pendingCompletion = context.Completion;
                _pendingEvents = new Queue<EventData>(context.Events);
                _currentContext = context.Context;

                TryPush();
            }

            private void TryPush()
            {
                // Wait for new messages
                if(_pendingEvents == null || _pendingEvents.Count == 0)
                    return;

                if (IsAvailable(_source.Out))
                {
                    Push(_source.Out, Tuple.Create(_currentContext, _pendingEvents.Dequeue()));

                    // We have processed all messages so we can handle more
                    if (_pendingEvents.Count == 0)
                        _pendingCompletion.TrySetResult(NotUsed.Instance);
                }
            }
        }

        #endregion

        private readonly bool _createCheckpointOnClose;

        public EventHubSource(bool createCheckpointOnClose)
        {
            _createCheckpointOnClose = createCheckpointOnClose;
            Shape = new SourceShape<Tuple<PartitionContext, EventData>>(Out);
        }

        public Outlet<Tuple<PartitionContext, EventData>> Out { get; } =
            new Outlet<Tuple<PartitionContext, EventData>>("EventHubSource.Out");

        public override SourceShape<Tuple<PartitionContext, EventData>> Shape { get; }

        public override ILogicAndMaterializedValue<IEventProcessor> CreateLogicAndMaterializedValue(
            Attributes inheritedAttributes)
        {
            var logic = new Logic(this);
            return new LogicAndMaterializedValue<IEventProcessor>(logic, logic);
        }
    }
}
