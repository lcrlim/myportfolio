﻿// <copyright file="GenericSynchronizingObject.cs" company="Nito Programs">
//     Copyright (c) 2009 Nito Programs.
// </copyright>

namespace Nito.Async
{
    using System;
    using System.ComponentModel;
    using System.Threading;

    /// <summary>
    /// Allows objects that use <see cref="ISynchronizeInvoke"/> (usually using a property named SynchronizingObject) to synchronize to a generic <see cref="SynchronizationContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>.NET framework types that use <see cref="ISynchronizeInvoke"/> include <see cref="System.Timers.Timer">System.Timers.Timer</see>, <see cref="System.Diagnostics.EventLog">System.Diagnostics.EventLog</see>, <see cref="System.Diagnostics.Process">System.Diagnostics.Process</see>, and <see cref="System.IO.FileSystemWatcher">System.IO.FileSystemWatcher</see>.</para>
    /// <para>This class does not invoke <see cref="SynchronizationContext.OperationStarted"/> or <see cref="SynchronizationContext.OperationCompleted"/>, so for some synchronization contexts, these may need to be called explicitly in addition to using this class. ASP.NET do require them to be called; Windows Forms, WPF, free threads, and <see cref="ActionDispatcher"/> do not.</para>
    /// </remarks>
    /// <threadsafety static="true" instance="true"/>
    /// <example>
    /// The following code example demonstrates how GenericSynchronizingObject may be used to redirect FileSystemWatcher events to an ActionThread:
    /// <code source="..\..\Source\Examples\DocumentationExamples\GenericSynchronizingObject\WithFileSystemWatcher.cs"/>
    /// The code example above produces this output:
    /// <code lang="None" title="Output">
    /// ActionThread thread ID is 3
    /// FileSystemWriter.Created thread ID is 3
    /// </code>
    /// </example>
    public sealed class GenericSynchronizingObject : ISynchronizeInvoke
    {
        /// <summary>
        /// The captured synchronization context.
        /// </summary>
        private SynchronizationContext synchronizationContext;

        /// <summary>
        /// The managed thread id of the synchronization context's specific associated thread, if any.
        /// </summary>
        private int? synchronizationContextThreadId;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericSynchronizingObject"/> class, binding to <see cref="SynchronizationContext.Current">SynchronizationContext.Current</see>.
        /// </summary>
        /// <example>
        /// The following code example demonstrates how GenericSynchronizingObject may be used to redirect FileSystemWatcher events to an ActionThread:
        /// <code source="..\..\Source\Examples\DocumentationExamples\GenericSynchronizingObject\WithFileSystemWatcher.cs"/>
        /// The code example above produces this output:
        /// <code lang="None" title="Output">
        /// ActionThread thread ID is 3
        /// FileSystemWriter.Created thread ID is 3
        /// </code>
        /// </example>
        public GenericSynchronizingObject()
        {
            // (This method is always invoked from a SynchronizationContext thread)
            this.synchronizationContext = SynchronizationContext.Current;
            if (this.synchronizationContext == null)
            {
                this.synchronizationContext = new SynchronizationContext();
            }

            if ((SynchronizationContextRegister.Lookup(this.synchronizationContext.GetType()) & SynchronizationContextProperties.SpecificAssociatedThread) == SynchronizationContextProperties.SpecificAssociatedThread)
            {
                this.synchronizationContextThreadId = Thread.CurrentThread.ManagedThreadId;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current thread must invoke a delegate.
        /// </summary>
        /// <remarks>
        /// <para>If there is not enough information about the synchronization context to determine this value, then this property evaluates to <c>false</c>. This is done because a cross-thread exception is easier to diagnose than a deadlock.</para>
        /// </remarks>
        public bool InvokeRequired
        {
            get
            {
                if (this.synchronizationContextThreadId != null)
                {
                    return this.synchronizationContextThreadId != Thread.CurrentThread.ManagedThreadId;
                }

                string name = this.synchronizationContext.GetType().Name;
                if (name == "SynchronizationContext")
                {
                    return !Thread.CurrentThread.IsThreadPoolThread;
                }

                // Unfortunately, there is no way to determine InvokeRequired for arbitrary contexts without specific associated threads.
                // So, we just return false. This will result in correct behavior on all existing SynchronizationContext implementations,
                //  but may cause a cross-threading exception if some weird new SynchronizationContext is invented in the future.
                return false;
            }
        }

        /// <summary>
        /// Starts the invocation of a delegate synchronized by the <see cref="SynchronizationContext"/> of the thread that created this <see cref="GenericSynchronizingObject"/>. A corresponding call to <see cref="EndInvoke"/> is not required.
        /// </summary>
        /// <param name="method">The delegate to run.</param>
        /// <param name="args">The arguments to pass to <paramref name="method"/>.</param>
        /// <returns>An <see cref="IAsyncResult"/> that can be used to detect completion of the delegate.</returns>
        /// <remarks>
        /// <para>If the <see cref="SynchronizationContext.Post"/> for this object's synchronization context is reentrant, then this method is also reentrant.</para>
        /// </remarks>
        public IAsyncResult BeginInvoke(Delegate method, object[] args)
        {
            // (This method may be invoked from any thread)
            IAsyncResult ret = new AsyncResult();

            // (The delegate passed to Post will run in the thread chosen by the SynchronizationContext)
            this.synchronizationContext.Post(
                (SendOrPostCallback)delegate(object state)
                {
                    AsyncResult result = (AsyncResult)state;
                    try
                    {
                        result.ReturnValue = method.DynamicInvoke(args);
                    }
                    catch (Exception ex)
                    {
                        result.Error = ex;
                    }

                    result.Done();
                },
                ret);
            return ret;
        }

        /// <summary>
        /// Waits for the invocation of a delegate to complete, and returns the result of the delegate. This may only be called once for a given <see cref="IAsyncResult"/> object.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult"/> returned from a call to <see cref="BeginInvoke"/>.</param>
        /// <returns>The result of the delegate.</returns>
        /// <remarks>
        /// <para>If the delegate raised an exception, then this method will raise a <see cref="System.Reflection.TargetInvocationException"/> with that exception as the <see cref="Exception.InnerException"/> property.</para>
        /// </remarks>
        public object EndInvoke(IAsyncResult result)
        {
            // (This method may be invoked from any thread)
            AsyncResult asyncResult = (AsyncResult)result;
            asyncResult.WaitForAndDispose();
            if (asyncResult.Error != null)
            {
                throw asyncResult.Error;
            }

            return asyncResult.ReturnValue;
        }

        /// <summary>
        /// Synchronously invokes a delegate synchronized with the <see cref="SynchronizationContext"/> of the thread that created this <see cref="GenericSynchronizingObject"/>.
        /// </summary>
        /// <param name="method">The delegate to invoke.</param>
        /// <param name="args">The parameters for <paramref name="method"/>.</param>
        /// <returns>The result of the delegate.</returns>
        /// <remarks>
        /// <para>If the <see cref="SynchronizationContext.Send"/> for this object's synchronization context is reentrant, then this method is also reentrant.</para>
        /// <para>If the delegate raises an exception, then this method will raise a <see cref="System.Reflection.TargetInvocationException"/> with that exception as the <see cref="Exception.InnerException"/> property.</para>
        /// </remarks>
        public object Invoke(Delegate method, object[] args)
        {
            // (This method may be invoked from any thread)
            ReturnValue ret = new ReturnValue();
            this.synchronizationContext.Send(
                delegate(object unusedState)
                {
                    try
                    {
                        ret.ReturnedValue = method.DynamicInvoke(args);
                    }
                    catch (Exception ex)
                    {
                        ret.Error = ex;
                    }
                },
                null);
            if (ret.Error != null)
            {
                throw ret.Error;
            }

            return ret.ReturnedValue;
        }

        /// <summary>
        /// A helper object that just wraps the return value, when the delegate is invoked synchronously.
        /// </summary>
        private sealed class ReturnValue
        {
            /// <summary>
            /// Gets or sets return value, if any. This is only valid if <see cref="Error"/> is not <c>null</c>. May be <c>null</c>, even if valid.
            /// </summary>
            public object ReturnedValue { get; set; }

            /// <summary>
            /// Gets or sets the error, if any. May be <c>null</c>.
            /// </summary>
            public Exception Error { get; set; }
        }

        // Note that our implementation of AsyncResult differs significantly from that presented in "Implementing the CLR Asynchronous
        //  Programming Model", MSDN 2007-03, Jeffrey Richter. They take a lock-free approach, while we use explicit locks.
        // Some of the major differences:
        //  1) Ours is simplified, not handling synchronous completion, user-defined states, or callbacks.
        //  2) We use a lock instead of interlocked variables for these reasons:
        //    a) Locks tend to scale better as the number of CPUs increase (they only affect a single thread while interlocked affects
        //       the instruction cache of every CPU).
        //    b) Code is easier to read and understand that there are no race conditions.
        //    c) We do handle the situation where a WaitHandle is created earlier but not immediately used for synchronization. This is
        //       rare in practice.
        //    d) Race conditions are handled more efficiently. This is also rare in practice.
        //  3) However, we do require the allocation of a lock for every AsyncResult instance, so our solution does use more resources.

        /// <summary>
        /// A helper object that holds the return value and also allows waiting for the asynchronous completion of a delegate.
        /// Note that calling <see cref="ISynchronizeInvoke.EndInvoke"/> is optional, and this class is optimized for that common use case.
        /// </summary>
        private sealed class AsyncResult : IAsyncResult
        {
            /// <summary>
            /// The wait handle, which may be null. Writes are synchronized using Interlocked access.
            /// </summary>
            private ManualResetEvent asyncWaitHandle;

            /// <summary>
            /// Whether the operation has completed. Synchronized using atomic reads/writes and Interlocked access.
            /// </summary>
            private bool isCompleted;

            /// <summary>
            /// Object used for synchronization.
            /// </summary>
            private object syncObject = new object();

            /// <summary>
            /// Gets or sets the return value. Must be set before calling <see cref="Done"/>.
            /// </summary>
            public object ReturnValue { get; set; }

            /// <summary>
            /// Gets or sets the error. Must be set before calling <see cref="Done"/>.
            /// </summary>
            public Exception Error { get; set; }

            /// <summary>
            /// Gets the user-defined state. Always returns <c>null</c>; user-defined state is not supported.
            /// </summary>
            /// <remarks>
            /// <para>This property may be accessed in an arbitrary thread context.</para>
            /// </remarks>
            public object AsyncState
            {
                get { return null; }
            }

            /// <summary>
            /// Gets a waitable handle for this operation.
            /// </summary>
            /// <remarks>
            /// <para>This property may be accessed in an arbitrary thread context.</para>
            /// </remarks>
            public WaitHandle AsyncWaitHandle
            {
                get
                {
                    lock (this.syncObject)
                    {
                        // If it already exists, return it
                        if (this.asyncWaitHandle != null)
                        {
                            return this.asyncWaitHandle;
                        }

                        // Create a new one
                        this.asyncWaitHandle = new ManualResetEvent(this.isCompleted);
                        return this.asyncWaitHandle;
                    }
                }
            }

            /// <summary>
            /// Gets a value indicating whether the operation completed synchronously. Always returns false; synchronous completion is not supported.
            /// </summary>
            /// <remarks>
            /// <para>This property may be accessed in an arbitrary thread context.</para>
            /// </remarks>
            public bool CompletedSynchronously
            {
                get { return false; }
            }

            /// <summary>
            /// Gets a value indicating whether this operation has completed.
            /// </summary>
            /// <remarks>
            /// <para>This property may be accessed in an arbitrary thread context.</para>
            /// </remarks>
            public bool IsCompleted
            {
                get
                {
                    lock (this.syncObject)
                    {
                        return this.isCompleted;
                    }
                }
            }

            /// <summary>
            /// Marks the AsyncResult object as done. Should only be called once.
            /// </summary>
            /// <remarks>
            /// <para>This method always runs in the SynchronizationContext thread.</para>
            /// </remarks>
            public void Done()
            {
                lock (this.syncObject)
                {
                    this.isCompleted = true;

                    // Set the wait handle, only if necessary
                    if (this.asyncWaitHandle != null)
                    {
                        this.asyncWaitHandle.Set();
                    }
                }
            }

            /// <summary>
            /// Waits for the pending operation to complete, if necessary, and frees all resources. Should only be called once.
            /// </summary>
            /// <remarks>
            /// <para>This method may run in an arbitrary thread context.</para>
            /// </remarks>
            public void WaitForAndDispose()
            {
                // First, do a simple check to see if it's completed
                if (this.IsCompleted)
                {
                    // Ensure the underlying wait handle is disposed if necessary
                    lock (this.syncObject)
                    {
                        if (this.asyncWaitHandle != null)
                        {
                            this.asyncWaitHandle.Close();
                            this.asyncWaitHandle = null;
                        }
                    }

                    return;
                }

                // Wait for the signal that it's completed, creating the signal if necessary
                this.AsyncWaitHandle.WaitOne();

                // Now that it's completed, dispose of the underlying wait handle
                lock (this.syncObject)
                {
                    this.asyncWaitHandle.Close();
                    this.asyncWaitHandle = null;
                }
            }
        }
    }
}