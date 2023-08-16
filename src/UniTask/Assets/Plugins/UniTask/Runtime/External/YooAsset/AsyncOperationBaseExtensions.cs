namespace Cysharp.Threading.Tasks
{
    using System;
    using YooAsset;
    using static Cysharp.Threading.Tasks.Internal.Error;

    public static class AsyncOperationBaseExtensions
    {
        public static UniTask.Awaiter GetAwaiter(this AsyncOperationBase handle)
        {
            return ToUniTask(handle).GetAwaiter();
        }

        public static UniTask ToUniTask(this AsyncOperationBase handle,
                                        IProgress<float> progress = null,
                                        PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            ThrowArgumentNullException(handle, nameof(handle));

            if (handle.IsDone)
            {
                return UniTask.CompletedTask;
            }

            return new UniTask(
                AsyncOperationBaserConfiguredSource.Create(
                    handle,
                    timing,
                    progress,
                    out var token
                ),
                token
            );
        }

        private sealed class AsyncOperationBaserConfiguredSource : IUniTaskSource,
                                                           IPlayerLoopItem,
                                                           ITaskPoolNode<AsyncOperationBaserConfiguredSource>
        {
            private static TaskPool<AsyncOperationBaserConfiguredSource> pool;

            private AsyncOperationBaserConfiguredSource nextNode;

            public ref AsyncOperationBaserConfiguredSource NextNode => ref this.nextNode;

            static AsyncOperationBaserConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(AsyncOperationBaserConfiguredSource), () => pool.Size);
            }

            private readonly Action<AsyncOperationBase> continuationAction;
            private AsyncOperationBase handle;
            private IProgress<float> progress;
            private bool completed;
            private UniTaskCompletionSourceCore<AsyncUnit> core;

            private AsyncOperationBaserConfiguredSource() { this.continuationAction = this.Continuation; }

            public static IUniTaskSource Create(AsyncOperationBase handle,
                                                PlayerLoopTiming timing,
                                                IProgress<float> progress,
                                                out short token)
            {
                if (!pool.TryPop(out var result))
                {
                    result = new AsyncOperationBaserConfiguredSource();
                }

                result.handle = handle;
                result.progress = progress;
                result.completed = false;
                TaskTracker.TrackActiveTask(result, 3);

                if (progress != null)
                {
                    PlayerLoopHelper.AddAction(timing, result);
                }

                handle.Completed += result.continuationAction;

                token = result.core.Version;

                return result;
            }

            private void Continuation(AsyncOperationBase _)
            {
                this.handle.Completed -= this.continuationAction;

                if (this.completed)
                {
                    this.TryReturn();
                }
                else
                {
                    this.completed = true;
                    if (this.handle.Status == EOperationStatus.Failed)
                    {
                        this.core.TrySetException(new Exception(this.handle.Error));
                    }
                    else
                    {
                        this.core.TrySetResult(AsyncUnit.Default);
                    }
                }
            }

            private bool TryReturn()
            {
                TaskTracker.RemoveTracking(this);
                this.core.Reset();
                this.handle = default;
                this.progress = default;
                return pool.TryPush(this);
            }

            public UniTaskStatus GetStatus(short token) => this.core.GetStatus(token);

            public void OnCompleted(Action<object> continuation, object state, short token)
            {
                this.core.OnCompleted(continuation, state, token);
            }

            public void GetResult(short token) { this.core.GetResult(token); }

            public UniTaskStatus UnsafeGetStatus() => this.core.UnsafeGetStatus();

            public bool MoveNext()
            {
                if (this.completed)
                {
                    this.TryReturn();
                    return false;
                }

                if (!this.handle.IsDone)
                {
                    this.progress?.Report(this.handle.Progress);
                }

                return true;
            }
        }
    }
}
