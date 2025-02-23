using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LockScreenImages;

public static class Extensions
{
    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// An updated string truncation helper.
    /// </summary>
    public static string Truncate(this string text, int maxLength, string mesial = "…")
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (maxLength > 0 && text.Length > maxLength)
        {
            var limit = maxLength / 2;
            if (limit > 1)
            {
                return String.Format("{0}{1}{2}", text.Substring(0, limit).Trim(), mesial, text.Substring(text.Length - limit).Trim());
            }
            else
            {
                var tmp = text.Length <= maxLength ? text : text.Substring(0, maxLength).Trim();
                return String.Format("{0}{1}", tmp, mesial);
            }
        }
        return text;
    }

    /// <summary>
    /// Creates an <see cref="Icon"/> from the given <see cref="Image"/>.
    /// </summary>
    /// <param name="image"><see cref="Image"/></param>
    /// <returns><see cref="Icon"/></returns>
    public static Icon? ToIcon(this Image image)
    {
        if (image is null)
            return null;

        // Create a bitmap from the image
        using (Bitmap bitmap = new Bitmap(image))
        {
            IntPtr hIcon = bitmap.GetHicon();

            // Create an icon from the HICON
            Icon icon = Icon.FromHandle(hIcon);

            // NOTE: We don't want to clean up the hIcon since accessing
            //       it could cause the System.ObjectDisposedException.
            // TODO: Fire off a delayed thread to clean up the reference
            //       after some time has elapsed - only if this method is
            //       used repeatedly/often.
            //DestroyIcon(hIcon);

            return icon;
        }
    }

    public static string HumanReadableSize(this long length)
    {
        const int unit = 1024;
        var mu = new List<string> { "B", "KB", "MB", "GB", "PT" };
        while (length > unit)
        {
            mu.RemoveAt(0);
            length /= unit;
        }
        return $"{length}{mu[0]}";
    }

    /// <summary>
    /// Determines the type of an image file by inspecting its header.
    /// </summary>
    /// <param name="filePath">Path to the image file.</param>
    /// <returns>The type of the image (e.g., "jpg", "png", "gif", etc.) or "Unknown" if not recognized.</returns>
    public static string DetermineType(this string imageFilePath, bool dumpHeader = false)
    {
        if (!File.Exists(imageFilePath)) { return string.Empty; }

        try
        {
            using (var stream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReader(stream))
                {
                    byte[] header = reader.ReadBytes(16);

                    if (dumpHeader)
                    {
                        Debug.WriteLine($"[IMAGE HEADER]");
                        foreach (var b in header)
                        {
                            if (b > 31)
                                Debug.Write($"{(char)b}");
                        }
                        Debug.WriteLine($"");
                    }
                    // Check for JPEG signature (bytes 6-9 should be 'J', 'F', 'I', 'F' or 'E' 'x' 'i' 'f')
                    if (header.Length >= 10 &&
                        header[6] == 'J' && header[7] == 'F' && header[8] == 'I' && header[9] == 'F')
                    {
                        return "jpg";
                    }
                    if (header.Length >= 9 && 
                        header[6] == 'E' && 
                       (header[7] == 'x' || header[7] == 'X') && 
                       (header[8] == 'i' || header[8] == 'I') && 
                       (header[9] == 'f' || header[7] == 'F'))
                    {
                        return "jpg";
                    }
                    if (header.Length >= 9 && 
                        header[6] == 'J' && 
                       (header[7] == 'P' || header[7] == 'p') && 
                       (header[8] == 'E' || header[8] == 'e') && 
                       (header[9] == 'G' || header[9] == 'g'))
                    {
                        return "jpg";
                    }
                    // Check for PNG signature (bytes 0-7: 89 50 4E 47 0D 0A 1A 0A)
                    if (header.Length >= 8 &&
                        header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E &&
                        header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A &&
                        header[6] == 0x1A && header[7] == 0x0A)
                    {
                        return "png";
                    }
                    if (header.Length >= 6 &&
                        header[0] == 0xFF && header[1] == 0xD8 &&
                        header[2] == 0xFF && header[3] == 0xDB &&
                        header[4] == 0x00 && header[5] == 0x84)
                    {
                        return "png"; // header-less PNG
                    }
                    // Check for GIF signature (bytes 0-2: "GIF")
                    if (header.Length >= 6 &&
                        header[0] == 'G' && header[1] == 'I' && header[2] == 'F')
                    {
                        return "gif";
                    }
                    // Check for TIFF signature (bytes 0-3: "II*" or "MM*")
                    if (header.Length >= 4 &&
                        ((header[0] == 'I' && header[1] == 'I' && header[2] == 0x2A && header[3] == 0x00) ||
                         (header[0] == 'M' && header[1] == 'M' && header[2] == 0x00 && header[3] == 0x2A)))
                    {
                        return "tiff";
                    }
                    // Check for WebP signature (bytes 0-3: "RIFF", bytes 8-11: "WEBP")
                    if (header.Length >= 12 &&
                        header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
                        header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
                    {
                        return "webp";
                    }
                    // Check for HEIC/HEIF signature (bytes 4-11: "ftypheic" or "ftypheif")
                    if (header.Length >= 12 &&
                        header[4] == 'f' && header[5] == 't' && header[6] == 'y' && header[7] == 'p' &&
                       (header[8] == 'h' && header[9] == 'e' && header[10] == 'i' && header[11] == 'c'))
                    {
                        return "heic";
                    }
                    if (header.Length >= 12 &&
                        header[4] == 'f' && header[5] == 't' && header[6] == 'y' && header[7] == 'p' &&
                       (header[8] == 'h' && header[9] == 'e' && header[10] == 'i' && header[11] == 'f'))
                    {
                        return "heif";
                    }
                    // Check for BMP signature (bytes 0-1: "BM")
                    if (header.Length >= 2 &&
                        header[0] == 'B' && header[1] == 'M')
                    {
                        return "bmp";
                    }
                    // Signature not defined
                    return "Unknown"; 
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] DetermineImageType: {ex.Message}");
        }

        return string.Empty;
    }

    #region [Task Helpers]
    public static async Task WithTimeoutAsync(this Task task, TimeSpan timeout)
    {
        if (task == await Task.WhenAny(task, Task.Delay(timeout))) { await task; }
    }

    public static async Task<T?> WithTimeoutAsync<T>(this Task<T> task, TimeSpan timeout, T? defaultValue = default)
    {
        if (task == await Task.WhenAny(task, Task.Delay(timeout)))
            return await task;

        return defaultValue;
    }

    public static async Task<TOut> AndThen<TIn, TOut>(this Task<TIn> inputTask, Func<TIn, Task<TOut>> mapping)
    {
        var input = await inputTask;
        return (await mapping(input));
    }

    public static async Task<TOut?> AndThen<TIn, TOut>(this Task<TIn> inputTask, Func<TIn, Task<TOut>> mapping, Func<Exception, TOut>? errorHandler = null)
    {
        try
        {
            var input = await inputTask;
            return await mapping(input);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] AndThen: {ex.Message}");
            if (errorHandler != null)
                return errorHandler(ex);

            throw; // Rethrow if no handler is provided
        }
    }

    /// <summary>
    /// Runs the specified asynchronous method with return type.
    /// NOTE: Will not catch exceptions generated by the task.
    /// </summary>
    /// <param name="asyncMethod">The asynchronous method to execute.</param>
    public static T RunSynchronously<T>(this Func<Task<T>> asyncMethod)
    {
        if (asyncMethod == null)
            throw new ArgumentNullException($"{nameof(asyncMethod)} cannot be null");

        var prevCtx = SynchronizationContext.Current;
        try
        {   // Invoke the function and alert the context when it completes.
            var t = asyncMethod();
            if (t == null)
                throw new InvalidOperationException("No task provided.");

            return t.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(prevCtx); }
    }

    /// <summary>
    /// Runs the specified asynchronous method without return type.
    /// NOTE: Will not catch exceptions generated by the task.
    /// </summary>
    /// <param name="asyncMethod">The asynchronous method to execute.</param>
    public static void RunSynchronously(this Func<Task> asyncMethod)
    {
        if (asyncMethod == null)
            throw new ArgumentNullException($"{nameof(asyncMethod)}");

        var prevCtx = SynchronizationContext.Current;
        try
        {   // Invoke the function and alert the context when it completes
            var t = asyncMethod();
            if (t == null)
                throw new InvalidOperationException("No task provided.");

            t.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(prevCtx); }
    }

    /// <summary>
    /// Chainable task helper.
    /// var result = await SomeLongAsyncFunction().WithTimeout(TimeSpan.FromSeconds(2));
    /// </summary>
    /// <typeparam name="TResult">the type of task result</typeparam>
    /// <returns><see cref="Task"/>TResult</returns>
    public async static Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout)
    {
        Task winner = await (Task.WhenAny(task, Task.Delay(timeout)));

        if (winner != task)
            throw new TimeoutException();

        return await task;   // Unwrap result/re-throw
    }

    /// <summary>
    /// Task extension to add a timeout.
    /// </summary>
    /// <returns>The task with timeout.</returns>
    /// <param name="task">Task.</param>
    /// <param name="timeoutInMilliseconds">Timeout duration in Milliseconds.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public async static Task<T> WithTimeout<T>(this Task<T> task, int timeoutInMilliseconds)
    {
        var retTask = await Task.WhenAny(task, Task.Delay(timeoutInMilliseconds))
            .ConfigureAwait(false);

#pragma warning disable CS8603 // Possible null reference return.
        return retTask is Task<T> ? task.Result : default;
#pragma warning restore CS8603 // Possible null reference return.
    }

    /// <summary>
    /// Chainable task helper.
    /// var result = await SomeLongAsyncFunction().WithCancellation(cts.Token);
    /// </summary>
    /// <typeparam name="TResult">the type of task result</typeparam>
    /// <returns><see cref="Task"/>TResult</returns>
    public static Task<TResult> WithCancellation<TResult>(this Task<TResult> task, CancellationToken cancelToken)
    {
        var tcs = new TaskCompletionSource<TResult>();
        var reg = cancelToken.Register(() => tcs.TrySetCanceled());
        task.ContinueWith(ant =>
        {
            reg.Dispose();
            if (ant.IsCanceled)
                tcs.TrySetCanceled();
            else if (ant.IsFaulted)
                tcs.TrySetException(ant.Exception?.InnerException ?? new Exception("Antecedent faulted."));
            else
                tcs.TrySetResult(ant.Result);
        });
        return tcs.Task;  // Return the TaskCompletionSource result
    }

    public static Task<T> WithAllExceptions<T>(this Task<T> task)
    {
        TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();

        task.ContinueWith(ignored =>
        {
            switch (task.Status)
            {
                case TaskStatus.Canceled:
                    Debug.WriteLine($"[TaskStatus.Canceled]");
                    tcs.SetCanceled();
                    break;
                case TaskStatus.RanToCompletion:
                    tcs.SetResult(task.Result);
                    //Debug.WriteLine($"[TaskStatus.RanToCompletion({task.Result})]");
                    break;
                case TaskStatus.Faulted:
                    // SetException will automatically wrap the original AggregateException
                    // in another one. The new wrapper will be removed in TaskAwaiter, leaving
                    // the original intact.
                    Debug.WriteLine($"[TaskStatus.Faulted: {task.Exception?.Message}]");
                    tcs.SetException(task.Exception ?? new Exception("Task faulted."));
                    break;
                default:
                    Debug.WriteLine($"[TaskStatus: Continuation called illegally.]");
                    tcs.SetException(new InvalidOperationException("Continuation called illegally."));
                    break;
            }
        });

        return tcs.Task;
    }

#pragma warning disable RECS0165 // Asynchronous methods should return a Task instead of void
    /// <summary>
    /// Attempts to await on the task and catches exception
    /// </summary>
    /// <param name="task">Task to execute</param>
    /// <param name="onException">What to do when method has an exception</param>
    /// <param name="continueOnCapturedContext">If the context should be captured.</param>
    public static async void SafeFireAndForget(this Task task, Action<Exception>? onException = null, bool continueOnCapturedContext = false)
#pragma warning restore RECS0165 // Asynchronous methods should return a Task instead of void
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (Exception ex) when (onException != null)
        {
            onException.Invoke(ex);
        }
        catch (Exception ex) when (onException == null)
        {
            Debug.WriteLine($"SafeFireAndForget: {ex.Message}");
        }
    }

    /// <summary>
    /// Task.Factory.StartNew (() => { throw null; }).IgnoreExceptions();
    /// </summary>
    public static void IgnoreExceptions(this Task task)
    {
        task.ContinueWith(t =>
        {
            var ignore = t.Exception;
            var inners = ignore?.Flatten()?.InnerExceptions;
            if (inners != null)
            {
                foreach (Exception ex in inners)
                    Debug.WriteLine($"[{ex.GetType()}]: {ex.Message}");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Gets the result of a <see cref="Task"/> if available, or <see langword="null"/> otherwise.
    /// </summary>
    /// <param name="task">The input <see cref="Task"/> instance to get the result for.</param>
    /// <returns>The result of <paramref name="task"/> if completed successfully, or <see langword="default"/> otherwise.</returns>
    /// <remarks>
    /// This method does not block if <paramref name="task"/> has not completed yet. Furthermore, it is not generic
    /// and uses reflection to access the <see cref="Task{TResult}.Result"/> property and boxes the result if it's
    /// a value type, which adds overhead. It should only be used when using generics is not possible.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? GetResultOrDefault(this Task task)
    {
        // Check if the instance is a completed Task
        if (
#if NETSTANDARD2_1
            task.IsCompletedSuccessfully
#else
            task.Status == TaskStatus.RanToCompletion
#endif
        )
        {
            // We need an explicit check to ensure the input task is not the cached
            // Task.CompletedTask instance, because that can internally be stored as
            // a Task<T> for some given T (e.g. on dotNET 5 it's VoidTaskResult), which
            // would cause the following code to return that result instead of null.
            if (task != Task.CompletedTask)
            {
                // Try to get the Task<T>.Result property. This method would've
                // been called anyway after the type checks, but using that to
                // validate the input type saves some additional reflection calls.
                // Furthermore, doing this also makes the method flexible enough to
                // cases whether the input Task<T> is actually an instance of some
                // runtime-specific type that inherits from Task<T>.
                PropertyInfo? propertyInfo =
#if NETSTANDARD1_4
                    task.GetType().GetRuntimeProperty(nameof(Task<object>.Result));
#else
                    task.GetType().GetProperty(nameof(Task<object>.Result));
#endif

                // Return the result, if possible
                return propertyInfo?.GetValue(task);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the result of a <see cref="Task{TResult}"/> if available, or <see langword="default"/> otherwise.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="Task{TResult}"/> to get the result for.</typeparam>
    /// <param name="task">The input <see cref="Task{TResult}"/> instance to get the result for.</param>
    /// <returns>The result of <paramref name="task"/> if completed successfully, or <see langword="default"/> otherwise.</returns>
    /// <remarks>This method does not block if <paramref name="task"/> has not completed yet.</remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? GetResultOrDefault<T>(this Task<T?> task)
    {
#if NETSTANDARD2_1
        return task.IsCompletedSuccessfully ? task.Result : default;
#else
        return task.Status == TaskStatus.RanToCompletion ? task.Result : default;
#endif
    }
    #endregion
}
