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
using System.Threading;
using System.Threading.Tasks;

namespace IntelOrca.AsyncProcessor
{
	/// <summary>
	/// Console application for testing the asynchronous processor.
	/// </summary>
	internal class Program
	{
		/// <summary>
		/// Random number generator for random processing delays.
		/// </summary>
		private static Random Random = new Random();

		/// <summary>
		/// Main entry point.
		/// </summary>
		/// <param name="args">The command line arguments.</param>
		private static void Main(string[] args)
		{
			Console.WriteLine("Ordered by completion");
			TestDemonstration(false);

			Console.WriteLine();
			Console.WriteLine("Ordered by input order");
			TestDemonstration(true);

			Console.ReadLine();
		}

		/// <summary>
		/// Test the asynchronous processor using random delays.
		/// </summary>
		/// <param name="maintainOrder"></param>
		private static void TestDemonstration(bool maintainOrder)
		{
			var processor = new AsyncProcessor<InputPacket, OutputPacket>(ProcessPacket, maintainOrder);
			var resultObserver = new ResultObserver();

			using (var resultObserverSubscription = processor.Subscribe(resultObserver)) {
				for (int i = 0; i < 8; i++) {
					var packet = new InputPacket(i);
					Console.WriteLine(packet);
					processor.OnNext(packet);

					Thread.Sleep(Random.Next(50, 150));
				}
				processor.OnCompleted();

				while (!resultObserver.Completed) { }
			}
		}

		/// <summary>
		/// The process function for the asynchronous processor to use for each input.
		/// </summary>
		/// <param name="packet">The input packet.</param>
		/// <returns>Task to return the processed input packet as an output packet.</returns>
		private static async Task<OutputPacket> ProcessPacket(InputPacket packet)
		{
			await Task.Delay(Random.Next(250, 500));
			return new OutputPacket(packet.Index);
		}
	}

	/// <summary>
	/// Class to observe the outputs from the asynchronous processor.
	/// </summary>
	internal class ResultObserver : IObserver<OutputPacket>
	{
		/// <summary>
		/// Gets whether the observer has been notified of a completion message or not.
		/// </summary>
		public bool Completed { get; private set; }

		/// <summary>
		/// Notifies the observer that the provider has finished sending push-based notifications.
		/// </summary>
		public void OnCompleted()
		{
			Console.WriteLine("Completed");
			Completed = true;
		}

		/// <summary>
		/// Notifies the observer that the provider has experienced an error condition.
		/// </summary>
		/// <param name="error">An object that provides additional information about the error.</param>
		public void OnError(Exception error)
		{
			Console.WriteLine("Error: ", error.Message);
		}

		/// <summary>
		/// Provides the observer with new data.
		/// </summary>
		/// <param name="value">The current notification information.</param>
		public void OnNext(OutputPacket value)
		{
			Console.WriteLine(value);
		}
	}

	/// <summary>
	/// Represents an input data example.
	/// </summary>
	internal class InputPacket
	{
		private readonly int _index;

		/// <summary>
		/// Gets the packet index.
		/// </summary>
		public int Index { get { return _index; } }

		/// <summary>
		/// Initialises a new instance of the <see cref="InputPacket"/> class.
		/// </summary>
		/// <param name="index">The index.</param>
		public InputPacket(int index)
		{
			_index = index;
		}

		/// <summary>
		/// Returns a <see cref="System.String" /> that represents this instance.
		/// </summary>
		/// <returns>A <see cref="System.String" /> that represents this instance.</returns>
		public override string ToString()
		{
			return String.Format("{0}, input packet", _index);
		}
	}

	/// <summary>
	/// Represents an output data example.
	/// </summary>
	internal class OutputPacket
	{
		private readonly int _index;

		/// <summary>
		/// Gets the packet index.
		/// </summary>
		public int Index { get { return _index; } }

		/// <summary>
		/// Initialises a new instance of the <see cref="OutputPacket"/> class.
		/// </summary>
		/// <param name="index">The index.</param>
		public OutputPacket(int index)
		{
			_index = index;
		}

		/// <summary>
		/// Returns a <see cref="System.String" /> that represents this instance.
		/// </summary>
		/// <returns>A <see cref="System.String" /> that represents this instance.</returns>
		public override string ToString()
		{
			return String.Format("{0}, output packet", _index);
		}
	}
}
