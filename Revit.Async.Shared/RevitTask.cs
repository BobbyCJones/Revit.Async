﻿#region Reference

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Revit.Async.Entities;
using Revit.Async.Extensions;
using Revit.Async.ExternalEvents;
using Revit.Async.Interfaces;
using Revit.Async.Utils;

#endregion

namespace Revit.Async
{
    /// <summary>
    ///     Provide some useful methods to support running Revit API code from any context
    /// </summary>
    public class RevitTask : IRevitTask
    {
        #region Fields

        private static ConcurrentDictionary<Type, FutureExternalEvent> _registeredExternalEvents;
        private        ConcurrentDictionary<Type, FutureExternalEvent> _scopedRegisteredExternalEvents;

        #endregion

        #region Properties

        /// <summary>
        ///     Store the external events registered globally
        /// </summary>
        private static ConcurrentDictionary<Type, FutureExternalEvent> RegisteredExternalEvents =>
            _registeredExternalEvents ?? (_registeredExternalEvents = new ConcurrentDictionary<Type, FutureExternalEvent>());

        /// <summary>
        ///     Store the external events registered in current scope
        /// </summary>
        private ConcurrentDictionary<Type, FutureExternalEvent> ScopedRegisteredExternalEvents =>
            _scopedRegisteredExternalEvents ?? (_scopedRegisteredExternalEvents = new ConcurrentDictionary<Type, FutureExternalEvent>());

        #endregion

        #region Interface Implementations

        /// <inheritdoc />
        public void Dispose()
        {
            _scopedRegisteredExternalEvents.Clear();
            _scopedRegisteredExternalEvents = null;
#if DEBUG
            Writer?.Close();
            Writer?.Dispose();
#endif
        }

        /// <inheritdoc />
        public Task<TResult> Raise<THandler, TParameter, TResult>(TParameter parameter)
            where THandler : IGenericExternalEventHandler<TParameter, TResult>
        {
            if (ScopedRegisteredExternalEvents.TryGetValue(typeof(THandler), out var futureExternalEvent))
            {
                return futureExternalEvent.RunAsync<TParameter, TResult>(parameter);
            }

            return TaskUtils.FromResult(default(TResult));
        }

        public Task<TResult> RaiseNew<THandler, TParameter, TResult>(TParameter parameter)
            where THandler : IGenericExternalEventHandler<TParameter, TResult>
        {
            if (ScopedRegisteredExternalEvents.TryGetValue(typeof(THandler), out var futureExternalEvent))
            {
                return (futureExternalEvent.Clone() as FutureExternalEvent)?.RunAsync<TParameter, TResult>(parameter);
            }

            return TaskUtils.FromResult(default(TResult));
        }

        /// <inheritdoc />
        public void Register<TParameter, TResult>(IGenericExternalEventHandler<TParameter, TResult> handler)
        {
            ScopedRegisteredExternalEvents.TryAdd(handler.GetType(), new FutureExternalEvent(handler));
        }

        #endregion

        #region Others

        /// <summary>
        ///     Always call this method ahead of time in Revit API context to make sure that <see cref="RevitTask" /> functions
        ///     properly
        /// </summary>
        public static void Initialize()
        {
            FutureExternalEvent.Initialize();
        }

        /// <summary>
        ///     Raise an <see cref="IGenericExternalEventHandler{TParameter,TResult}" /> and get the result.
        ///     If a the handler is not registered globally, find the RevitTask instance to raise it
        /// </summary>
        /// <typeparam name="THandler">The type of the <see cref="IGenericExternalEventHandler{TParameter,TResult}" /></typeparam>
        /// <typeparam name="TParameter">
        ///     The type of the parameter that
        ///     <see cref="IGenericExternalEventHandler{TParameter,TResult}" /> accepts
        /// </typeparam>
        /// <typeparam name="TResult">
        ///     The type of result that <see cref="IGenericExternalEventHandler{TParameter,TResult}" />
        ///     generates
        /// </typeparam>
        /// <param name="parameter">
        ///     The parameter passed to the <see cref="IGenericExternalEventHandler{TParameter,TResult}" />
        /// </param>
        /// <returns>The result generated by <see cref="IGenericExternalEventHandler{TParameter,TResult}" /></returns>
        public static Task<TResult> RaiseGlobal<THandler, TParameter, TResult>(TParameter parameter)
            where THandler : IGenericExternalEventHandler<TParameter, TResult>
        {
            if (RegisteredExternalEvents.TryGetValue(typeof(THandler), out var futureExternalEvent))
            {
                return futureExternalEvent.RunAsync<TParameter, TResult>(parameter);
            }

            return TaskUtils.FromResult(default(TResult));
        }

        /// <summary>
        ///     Raise an <see cref="IGenericExternalEventHandler{TParameter,TResult}" /> and get the result.
        ///     If a the handler is not registered globally, find the RevitTask instance to raise it
        /// </summary>
        /// <typeparam name="THandler">The type of the <see cref="IGenericExternalEventHandler{TParameter,TResult}" /></typeparam>
        /// <typeparam name="TParameter">
        ///     The type of the parameter that
        ///     <see cref="IGenericExternalEventHandler{TParameter,TResult}" /> accepts
        /// </typeparam>
        /// <typeparam name="TResult">
        ///     The type of result that <see cref="IGenericExternalEventHandler{TParameter,TResult}" />
        ///     generates
        /// </typeparam>
        /// <param name="parameter">
        ///     The parameter passed to the <see cref="IGenericExternalEventHandler{TParameter,TResult}" />
        /// </param>
        /// <returns>The result generated by <see cref="IGenericExternalEventHandler{TParameter,TResult}" /></returns>
        public static Task<TResult> RaiseGlobalNew<THandler, TParameter, TResult>(TParameter parameter)
            where THandler : IGenericExternalEventHandler<TParameter, TResult>
        {
            if (RegisteredExternalEvents.TryGetValue(typeof(THandler), out var futureExternalEvent))
            {
                return (futureExternalEvent.Clone() as FutureExternalEvent)?.RunAsync<TParameter, TResult>(parameter);
            }

            return TaskUtils.FromResult(default(TResult));
        }

        /// <summary>
        ///     Register an <see cref="IGenericExternalEventHandler{TParameter,TResult}" /> to globally to be
        ///     raised. If a global handler is not what you want, make use of a RevitTask instance to register a scoped handler
        /// </summary>
        /// <typeparam name="TParameter">
        ///     The type of the parameter that
        ///     <see cref="IGenericExternalEventHandler{TParameter,TResult}" /> accepts
        /// </typeparam>
        /// <typeparam name="TResult">
        ///     The type of result that <see cref="IGenericExternalEventHandler{TParameter,TResult}" />
        ///     generates
        /// </typeparam>
        /// <param name="handler">The instance of <see cref="IGenericExternalEventHandler{TParameter,TResult}" /></param>
        public static void RegisterGlobal<TParameter, TResult>(IGenericExternalEventHandler<TParameter, TResult> handler)
        {
            RegisteredExternalEvents.TryAdd(handler.GetType(), new FutureExternalEvent(handler));
        }

        /// <summary>
        ///     Running Revit API code and get the result asynchronously
        /// </summary>
        /// <typeparam name="TResult">The type of the Result</typeparam>
        /// <param name="function">The delegate method wraps all the Revit API code with no argument</param>
        /// <returns></returns>
        public static Task<TResult> RunAsync<TResult>(Func<TResult> function)
        {
            return RunAsync(app => function());
        }

        /// <summary>
        ///     Running Revit API code and get the result asynchronously
        /// </summary>
        /// <typeparam name="TResult">The type of the Result</typeparam>
        /// <param name="function">The delegate method wraps all the Revit API code with <see cref="UIApplication" /> as argument</param>
        /// <returns>The result</returns>
        public static Task<TResult> RunAsync<TResult>(Func<UIApplication, TResult> function)
        {
            var handler             = new SyncDelegateExternalEventHandler<TResult>();
            var futureExternalEvent = new FutureExternalEvent(handler);
            return futureExternalEvent.RunAsync<Func<UIApplication, TResult>, TResult>(function);
        }

        /// <summary>
        ///     Running Revit API code and get the result asynchronously
        /// </summary>
        /// <typeparam name="TResult">The type of the Result</typeparam>
        /// <param name="function">
        ///     The delegate method wraps all the Revit API code and some other asynchronous processes with no
        ///     argument
        /// </param>
        /// <returns>The result</returns>
        public static Task<TResult> RunAsync<TResult>(Func<Task<TResult>> function)
        {
            return RunAsync(_ => function());
        }

        /// <summary>
        ///     Running Revit API code and get the result asynchronously
        /// </summary>
        /// <typeparam name="TResult">The type of the Result</typeparam>
        /// <param name="function">
        ///     The delegate method wraps all the Revit API code and some other asynchronous processes with
        ///     <see cref="UIApplication" /> as argument
        /// </param>
        /// <returns></returns>
        public static Task<TResult> RunAsync<TResult>(Func<UIApplication, Task<TResult>> function)
        {
            var handler             = new AsyncDelegateExternalEventHandler<TResult>();
            var futureExternalEvent = new FutureExternalEvent(handler);
            return futureExternalEvent.RunAsync<Func<UIApplication, Task<TResult>>, TResult>(function);
        }

        /// <summary>
        ///     Running Revit API code asynchronously
        /// </summary>
        /// <param name="action">The delegate method wraps all the Revit API code</param>
        /// <returns>The task indicating whether the execution has completed</returns>
        public static Task RunAsync(Action action)
        {
            return RunAsync(_ => action());
        }

        /// <summary>
        ///     Running Revit API code asynchronously
        /// </summary>
        /// <param name="action">The delegate method wraps all the Revit API code with <see cref="UIApplication" /> as argument</param>
        /// <returns>The task indicating whether the execution has completed</returns>
        public static Task RunAsync(Action<UIApplication> action)
        {
            return RunAsync(app =>
            {
                action(app);
                return (object) null;
            });
        }

        /// <summary>
        ///     Running Revit API code asynchronously
        /// </summary>
        /// <param name="function">The delegate method wraps all the Revit API code and some other asynchronous processes</param>
        /// <returns>The task indicating whether the execution has completed</returns>
        public static Task RunAsync(Func<Task> function)
        {
            return RunAsync(_ => function());
        }

        /// <summary>
        ///     Running Revit API code asynchronously
        /// </summary>
        /// <param name="function">
        ///     The delegate method wraps all the Revit API code and some other asynchronous processes with
        ///     <see cref="UIApplication" /> as argument
        /// </param>
        /// <returns></returns>
        public static Task RunAsync(Func<UIApplication, Task> function)
        {
            return RunAsync(app => function(app).ContinueWith(task => (object) null));
        }

        [Conditional("DEBUG")]
        internal static void Log(object obj)
        {
#if DEBUG
            try
            {
                Writer.WriteLine($"[{DateTime.Now}] {obj}");
                Writer.Flush();
            }
            catch
            {
                // ignored
            }
#endif
        }

        #endregion

#if DEBUG
        private static StreamWriter Writer { get; set; }
        /// <summary>
        ///     Always call this method ahead of time in Revit API context to make sure that <see cref="RevitTask" /> functions
        ///     properly
        /// </summary>
        public static void Initialize(string logFile)
        {
            if (Writer == null)
            {
                if (!string.IsNullOrWhiteSpace(logFile))
                {
                    Writer = new StreamWriter(logFile);
                }
            }

            Initialize();
        }
#endif
    }
}