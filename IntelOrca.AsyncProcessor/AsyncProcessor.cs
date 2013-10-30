#region Copyright (c) 2013 Ted John
// The MIT License (MIT)
// 
// Copyright (c) 2013 Ted John
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IntelOrca.AsyncProcessor
{
	/// <summary>
	/// Class for processing a set of inputs in parallel as they are published. Outputs from the processor inputs are then
	/// published to subscribers either in the order the inputs were given or the order in which they completed.
	/// </summary>
	/// <typeparam name="Tin">The input data type.</typeparam>
	/// <typeparam name="Tout">The processed input output data type.</typeparam>
	public sealed class AsyncProcessor<Tin, Tout> : IObserver<Tin>, IObservable<Tout>
	{
		/// <summary>
		/// Delegate for the process function handler.
		/// </summary>
		/// <param name="input">The input data type.</param>
		/// <returns>A task returning the output data type as a result from processing the input data type.</returns>
		public delegate Task<Tout> ProcessFunc(Tin input);

		private readonly List<Task<Tout>> _processingTasks = new List<Task<Tout>>();
		private readonly List<IObserver<Tout>> _subscribers = new List<IObserver<Tout>>();
		private readonly ProcessFunc _processFunction;
		private readonly bool _maintainInputOrder;

		private object _processingTasksSync = new object();
		private bool _inputCompleted;
		private int _processedInputs;
		private int _currentPublishIndex;
		private int _numPublishedOutputs;

		/// <summary>
		/// Gets the processing function handler.
		/// </summary>
		public ProcessFunc ProcessFunction { get { return _processFunction; } }

		/// <summary>
		/// Gets whether the processed input outputs are published in the same order as the inputs were published to the
		/// processor or not.
		/// </summary>
		public bool MaintainInputOrder { get { return _maintainInputOrder; } }

		/// <summary>
		/// Gets the number of inputs which have been published to the processor.
		/// </summary>
		public int PublishedInputs { get { return _processingTasks.Count; } }

		/// <summary>
		/// Gets the number of inputs that have been processed.
		/// </summary>
		public int ProcessedInputs { get { return _processedInputs; } }

		/// <summary>
		/// Gets the number of processed input outputs that have been published to the subscribers.
		/// </summary>
		public int PublishedOutputs { get { return _numPublishedOutputs; } }

		/// <summary>
		/// Initialises a new instance of the <see cref="AsyncProcessor{Tin, Tout}"/> class.
		/// </summary>
		/// <param name="processFunction">The process function handler.</param>
		/// <param name="maintainInputOrder">
		/// Whether to publish the processed input outputs in the order the inputs were
		/// published.
		/// </param>
		public AsyncProcessor(ProcessFunc processFunction, bool maintainInputOrder = false)
		{
			_processFunction = processFunction;
			_maintainInputOrder = maintainInputOrder;
		}

		/// <summary>
		/// Asynchronously process an input using the process function handler.
		/// </summary>
		/// <param name="input">The input data.</param>
		/// <returns></returns>
		private async Task ProcessInputAsync(Tin input)
		{
			int index;

			// Run the task asynchronously
			Task<Tout> task = _processFunction(input);

			// Add the task to the processing list
			lock (_processingTasksSync) {
				index = _processingTasks.Count;
				_processingTasks.Add(task);
			}

			// Wait for the task to finish
			await task;

			// Trigger the process aftermath
			OnInputProcessed(index);
		}

		/// <summary>
		/// Called when a input has been processed.
		/// </summary>
		/// <param name="index">The input index.</param>
		private void OnInputProcessed(int index)
		{
			_processedInputs++;

			// Check if we need to maintain the order the inputs were published
			if (_maintainInputOrder) {
				// Publish the outputs of all completed processing tasks until we reach a incomplete task in the processing list
				for (int i = _currentPublishIndex; i < _processingTasks.Count; i++) {
					if (!_processingTasks[i].IsCompleted)
						break;
					PublishOutput(_processingTasks[i].Result);
				}
			} else {
				// Publish the output right away
				PublishOutput(_processingTasks[index].Result);
			}
		}

		/// <summary>
		/// Publishes a processed input output to the subscribers.
		/// </summary>
		/// <param name="output">The processed input output.</param>
		private void PublishOutput(Tout output)
		{
			// Give output to all subscribers
			foreach (IObserver<Tout> subscriber in _subscribers)
				subscriber.OnNext(output);

			// Increment counts
			_currentPublishIndex++;
			_numPublishedOutputs++;

			// If we have output all the processed inputs, let the subscribers know we have completed
			if (_inputCompleted && _numPublishedOutputs == _processingTasks.Count)
				foreach (IObserver<Tout> subscriber in _subscribers)
					subscriber.OnCompleted();
		}

		/// <summary>
		/// Notifies the observer that the provider has finished sending push-based notifications.
		/// </summary>
		public void OnCompleted()
		{
			_inputCompleted = true;
		}

		/// <summary>
		/// Notifies the observer that the provider has experienced an error condition.
		/// </summary>
		/// <param name="error">An object that provides additional information about the error.</param>
		/// <exception cref="System.NotImplementedException"></exception>
		public void OnError(Exception error)
		{
			// Pass the error through to the subscribers
			foreach (IObserver<Tout> subscriber in _subscribers)
				subscriber.OnError(error);
		}

		/// <summary>
		/// Provides the observer with new data.
		/// </summary>
		/// <param name="value">The current notification information.</param>
		public async void OnNext(Tin value)
		{
			// Check if we have been given a new input after the input stream was supposedly completed
			if (_inputCompleted)
				throw new InvalidOperationException("Input stream has been completed.");

			// Process the input
			await ProcessInputAsync(value);
		}

		/// <summary>
		/// Subscribes the specified observer.
		/// </summary>
		/// <param name="observer">The observer.</param>
		/// <returns>An IDisposable that when disposed will unsubscribe the observer.</returns>
		public IDisposable Subscribe(IObserver<Tout> observer)
		{
			_subscribers.Add(observer);
			return new Unsubscriber(this, observer);
		}

		/// <summary>
		/// Unsubscribes an observer.
		/// </summary>
		/// <param name="observer">The observer to unsubscribe.</param>
		private void Unsubscribe(IObserver<Tout> observer)
		{
			_subscribers.Remove(observer);
		}

		/// <summary>
		/// Class used to unsubscribe an observer using the IDisposable interface.
		/// </summary>
		private class Unsubscriber : IDisposable
		{
			private readonly AsyncProcessor<Tin, Tout> _asyncProcessor;
			private readonly IObserver<Tout> _observer;

			/// <summary>
			/// Initialises a new instance of the <see cref="Unsubscriber"/> class.
			/// </summary>
			/// <param name="asyncProcessor">The asynchronous processor.</param>
			/// <param name="observer">The observer.</param>
			public Unsubscriber(AsyncProcessor<Tin, Tout> asyncProcessor, IObserver<Tout> observer)
			{
				_asyncProcessor = asyncProcessor;
				_observer = observer;
			}

			/// <summary>
			/// Unsubscribes the observer.
			/// </summary>
			public void Dispose()
			{
				_asyncProcessor.Unsubscribe(_observer);
			}
		}
	}
}
