using System;
using System.Collections.Generic;

namespace ChakraCoreHost
{
	public class Zxcvbn : IDisposable
	{
		JavascriptEngine engine;
		public Zxcvbn()
		{
			engine = new JavascriptEngine("zxcvbn.js");
		}
		public Zxcvbn GetZxcvbn(string password)
		{
			var result = engine.CallFunction<Zxcvbn>("zxcvbn", new System.Tuple<object, System.Type>[]
				{
				new System.Tuple<object, System.Type>(password, typeof(string))
				});
				return Assign(result);
		}
		private Zxcvbn Assign(Zxcvbn instance)
		{
			score = instance.score;
			feedback = instance.feedback;
			entropy = instance.entropy;
			return this;
		}
		public int score;
		public Feedback feedback;
		public double entropy;
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);

		}
		private bool disposed = false;
		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing)
				{
					engine.Dispose();
				}
			}
			disposed = true;
		}
		~Zxcvbn()
		{
			Dispose(false);
		}
	}
	public class Feedback
	{
		public string warning;
		public string[] suggestions;
	}
}
