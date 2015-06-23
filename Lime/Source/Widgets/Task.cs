﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime
{
	using EnumType = IEnumerator<object>;

	/// <summary>
	/// Задача (таск). Бывает, что нужно задать какую-нибудь последовательность действий и ждать окончания ее выполнения.
	/// Задачи основаны на перечислителях (IEnumerator<object>) и их операторе yield return
	/// </summary>
	public class Task : IDisposable
	{
		public static long TotalTasksUpdated = 0;
		public static bool ProfilingEnabled;
		private static Dictionary<Type, ProfileEntry> profile = new Dictionary<Type, ProfileEntry>();

		struct ProfileEntry
		{
			public long MemoryAllocated;
			public int CallCount;
			public int TaskCount;
		}

		public abstract class WaitPredicate
		{
			public float TotalTime;
			public abstract bool Evaluate();
		}

		public object Tag { get; set; }

		/// <summary>
		/// Возвращает true, если задача выполнена
		/// </summary>
		public bool Completed { get { return stack.Count == 0; } }
		
		private float waitTime;
		private WaitPredicate waitPredicate;
		private Stack<EnumType> stack = new Stack<EnumType>();

		public Task(EnumType e, object tag = null)
		{
			Tag = tag;
			stack.Push(e);
			var type = e.GetType();
			ProfileEntry pe;
			profile.TryGetValue(type, out pe);
			pe.TaskCount++;
			profile[type] = pe;
		}

		public override string ToString()
		{
			return stack.Peek().GetType().ToString();
		}

		public void Advance(float delta)
		{
			if (ProfilingEnabled) {
				var type = stack.Peek().GetType();
				var memoryAllocated = System.GC.GetTotalMemory(forceFullCollection: false);
				try {
					AdvanceHelper(delta);
				} finally {
					memoryAllocated = System.GC.GetTotalMemory(forceFullCollection: false) - memoryAllocated;
					ProfileEntry pe;
					profile.TryGetValue(type, out pe);
					pe.CallCount++;
					if (memoryAllocated > 0) {
						pe.MemoryAllocated += memoryAllocated;
					}
					profile[type] = pe;
				}
			} else {
				AdvanceHelper(delta);
			}
		}

		private void AdvanceHelper(float delta)
		{
			TotalTasksUpdated++;
			if (waitTime > 0) {
				waitTime -= delta;
				return;
			}
			if (waitPredicate != null) {
				waitPredicate.TotalTime += delta;
				if (waitPredicate.Evaluate()) {
					return;
				}
				waitPredicate = null;
			}
			var e = stack.Peek();
			if (e.MoveNext()) {
				HandleYieldedResult(e.Current);
			} else if (!Completed) {
				stack.Pop();
			}
		}

		public void Dispose()
		{
			while (stack.Count > 0) {
				var e = stack.Pop();
				e.Dispose();
			}
			waitPredicate = null;
		}

		private void HandleYieldedResult(object result)
		{
			if (result == null) {
				waitTime = 0;
			} else if (result is int) {
				waitTime = (int)result;
			} else if (result is float) {
				waitTime = (float)result;
			} else if (result is IEnumerator<object>) {
				stack.Push(result as IEnumerator<object>);
				Advance(0);
			} else if (result is WaitPredicate) {
				waitPredicate = result as WaitPredicate;
			} else if (result is Lime.Node) {
				waitPredicate = WaitForAnimation(result as Lime.Node);
			} else if (result is IEnumerable<object>) {
				throw new Lime.Exception("Use IEnumerator<object> instead of IEnumerable<object> for " + result);
			} else {
				throw new Lime.Exception("Invalid object yielded " + result);
			}
		}

		public static WaitPredicate WaitWhile(Func<bool> predicate)
		{
			return new BooleanWaitPredicate() { Preducate = predicate };
		}

		public static WaitPredicate WaitWhile(Func<float, bool> timePredicate)
		{
			return new TimeWaitPredicate() { Preducate = timePredicate };
		}
		
		public static WaitPredicate WaitForAnimation(Lime.Node node)
		{
			return new AnimationWaitPredicate() { Node = node };
		}

		private class AnimationWaitPredicate : WaitPredicate
		{
			public Node Node;

			public override bool Evaluate() { return Node.IsRunning; }
		}

		private class BooleanWaitPredicate : WaitPredicate
		{
			public Func<bool> Preducate;

			public override bool Evaluate() { return Preducate(); }
		}

		private class TimeWaitPredicate : WaitPredicate
		{
			public Func<float, bool> Preducate;

			public override bool Evaluate() { return Preducate(TotalTime); }
		}

		/// <summary>
		/// Выполняет задачу асинхронно в другом потоке. Возвращает null до тех пор, пока задача не будет выполнена или отменена
		/// </summary>
		/// <param name="action">Действия, которые должны быть выполнены</param>
		public static IEnumerator<object> ExecuteAsync(Action action)
		{
#if UNITY
			throw new NotImplementedException();
#else
			var t = new System.Threading.Tasks.Task(action);
			t.Start();
			while (!t.IsCompleted && !t.IsCanceled && !t.IsFaulted) {
				yield return null;
			}
#endif
		}

		public static void DumpProfile(System.IO.TextWriter writer)
		{
			var items = profile.Select(p => new { 
				Method = p.Key.ToString(), 
				Memory = p.Value.MemoryAllocated, 
				CallCount = p.Value.CallCount,
				TaskCount = p.Value.TaskCount,
			}).OrderByDescending(a => a.Memory);
			writer.WriteLine("Memory allocated\tCall count\tMethod Name");
			writer.WriteLine("===================================================================================================");
			foreach (var i in items) {
				writer.WriteLine("{0:N0}\t\t\t{1:N0}\t\t{2}\t\t{3}", i.Memory, i.CallCount, i.TaskCount, i.Method);
			}
		}
	}
}
