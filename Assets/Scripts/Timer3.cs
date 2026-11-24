using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Timer3
{
    private readonly float timeSeconds;
    private readonly bool isLoop;

    public event Action OnEnd;

    public Timer3(float timeSeconds, bool isLoop = false)
    {
        this.timeSeconds = Mathf.Max(timeSeconds, 0.001f);
        this.isLoop = isLoop;
    }

    public void Start(CancellationToken token)
    {
        WaitTimeAsync(token).Forget();
    }

    private async UniTaskVoid WaitTimeAsync(CancellationToken token)
    {
        try
        {
            do
            {
                await UniTask.Delay(TimeSpan.FromSeconds(timeSeconds), delayTiming: PlayerLoopTiming.Update, cancellationToken: token);

                OnEnd?.Invoke();

            } while (isLoop);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
