using ChakraCoreHost.Hosting;
using ChakraHost.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChakraCoreHost
{
	public class JavascriptEngine : IDisposable
	{
		string script;
		const string scriptPath = "script.js";
		public JavaScriptRuntime runtime;
		/// <summary>
		/// Script dispatcher
		/// </summary>
		private readonly ScriptDispatcher _dispatcher = new ScriptDispatcher();
		public JavascriptEngine(string scriptPath)
		{
			script = File.ReadAllText(scriptPath);
			runtime = JavaScriptRuntime.Create();
		}/// <summary>
		 /// Adds a reference to the value
		 /// </summary>
		 /// <param name="value">The value</param>
		private static void AddReferenceToValue(JavaScriptValue value)
		{
			if (CanHaveReferences(value))
			{
				value.AddRef();
			}
		}
		private static bool CanHaveReferences(JavaScriptValue value)
		{
			JavaScriptValueType valueType = value.ValueType;

			switch (valueType)
			{
				case JavaScriptValueType.Null:
				case JavaScriptValueType.Undefined:
				case JavaScriptValueType.Boolean:
					return false;
				default:
					return true;
			}
		}
		/// <summary>
		/// Removes a reference to the value
		/// </summary>
		/// <param name="value">The value</param>
		private static void RemoveReferenceToValue(JavaScriptValue value)
		{
			if (CanHaveReferences(value))
			{
				value.Release();
			}
		}
		public T CallFunction<T>(string functionName, Tuple<object, Type>[] parameters)
		{
			return _dispatcher.Invoke(() =>
			{
				var context = CreateHostContext(runtime, new string[0], 0); //arguments, commandLineArguments.ArgumentsStart);
				using (new JavaScriptContext.Scope(context))
				{
					JavaScriptContext.RunScript(script, currentSourceContext++, scriptPath);
					JavaScriptValue strResult;
					try
					{
						var jsonObject = JavaScriptValue.GlobalObject.GetProperty(JavaScriptPropertyId.FromString("JSON"));
						var stringify = jsonObject.GetProperty(JavaScriptPropertyId.FromString("stringify"));
						var parse = jsonObject.GetProperty(JavaScriptPropertyId.FromString("parse"));

						var zxcvbn = JavaScriptValue.GlobalObject.GetProperty(JavaScriptPropertyId.FromString(functionName));
						var javascriptParameters = CreateParameters(parameters);
						foreach (JavaScriptValue processedArg in javascriptParameters)
						{
							AddReferenceToValue(processedArg);
						}
						var result = zxcvbn.CallFunction(javascriptParameters.ToArray());
						foreach (JavaScriptValue processedArg in javascriptParameters)
						{
							RemoveReferenceToValue(processedArg);
						}
						strResult = stringify.CallFunction(JavaScriptValue.GlobalObject, result);
						return JsonConvert.DeserializeObject<T>(strResult.ConvertToString().ToString());

					}
					catch (JavaScriptScriptException e)
					{
						//log
						throw;
					}
					catch (Exception e)
					{
						//log
						throw;
					}
				}
			});
			

		}

		private static List<JavaScriptValue> CreateParameters(Tuple<object, Type>[] parameters)
		{
			var javascriptParameters = new List<JavaScriptValue>();
			javascriptParameters.Add(JavaScriptValue.GlobalObject);
			foreach (var parameter in parameters)
			{
				try
				{
					switch (parameter.Item2.FullName)
					{
						case "System.String":
							javascriptParameters.Add(JavaScriptValue.FromString(parameter.Item1.ToString()));
							break;
						case "System.Int32":
							javascriptParameters.Add(JavaScriptValue.FromInt32((int)parameter.Item1));
							break;
						case "System.Double":
							javascriptParameters.Add(JavaScriptValue.FromDouble((double)parameter.Item1));
							break;
						case "System.Boolean":
							javascriptParameters.Add(JavaScriptValue.FromBoolean((bool)parameter.Item1));
							break;
						default:
							throw new ArgumentException(string.Concat("parameter ", parameter.Item1, " of type ", parameter.Item2.FullName, "is not supported"));
					}
				}
				catch (InvalidCastException ex)
				{
					//log
					throw;
				}

			}
			return javascriptParameters;
		}

		private static JavaScriptContext CreateHostContext(JavaScriptRuntime runtime, string[] arguments, int argumentsStart)
		{
			//
			// Create the context. Note that if we had wanted to start debugging from the very
			// beginning, we would have called JsStartDebugging right after context is created.
			//

			JavaScriptContext context = runtime.CreateContext();

			//
			// Now set the execution context as being the current one on this thread.
			//

			using (new JavaScriptContext.Scope(context))
			{
				//
				// Create the host object the script will use.
				//

				JavaScriptValue hostObject = JavaScriptValue.CreateObject();

				//
				// Get the global object
				//

				JavaScriptValue globalObject = JavaScriptValue.GlobalObject;

				//
				// Get the name of the property ("host") that we're going to set on the global object.
				//

				JavaScriptPropertyId hostPropertyId = JavaScriptPropertyId.FromString("host");

				//
				// Set the property.
				//

				globalObject.SetProperty(hostPropertyId, hostObject, true);

				//
				// Now create the host callbacks that we're going to expose to the script.
				//

				DefineHostCallback(hostObject, "echo", echoDelegate, IntPtr.Zero);
				DefineHostCallback(hostObject, "runScript", runScriptDelegate, IntPtr.Zero);

				//
				// Create an array for arguments.
				//

				JavaScriptValue hostArguments = JavaScriptValue.CreateArray((uint)(arguments.Length - argumentsStart));

				for (int index = argumentsStart; index < arguments.Length; index++)
				{
					//
					// Create the argument value.
					//

					JavaScriptValue argument = JavaScriptValue.FromString(arguments[index]);

					//
					// Create the index.
					//

					JavaScriptValue indexValue = JavaScriptValue.FromInt32(index - argumentsStart);

					//
					// Set the value.
					//

					hostArguments.SetIndexedProperty(indexValue, argument);
				}

				//
				// Get the name of the property that we're going to set on the host object.
				//

				JavaScriptPropertyId argumentsPropertyId = JavaScriptPropertyId.FromString("arguments");

				//
				// Set the arguments property.
				//

				hostObject.SetProperty(argumentsPropertyId, hostArguments, true);
			}

			return context;
		}
		private static JavaScriptSourceContext currentSourceContext = JavaScriptSourceContext.FromIntPtr(IntPtr.Zero);

		// We have to hold on to the delegates on the managed side of things so that the
		// delegates aren't collected while the script is running.
		private static readonly JavaScriptNativeFunction echoDelegate = Echo;
		private static readonly JavaScriptNativeFunction runScriptDelegate = RunScript;

		private static void ThrowException(string errorString)
		{
			// We ignore error since we're already in an error state.
			JavaScriptValue errorValue = JavaScriptValue.FromString(errorString);
			JavaScriptValue errorObject = JavaScriptValue.CreateError(errorValue);
			JavaScriptContext.SetException(errorObject);
		}

		private static JavaScriptValue Echo(JavaScriptValue callee, bool isConstructCall, JavaScriptValue[] arguments, ushort argumentCount, IntPtr callbackData)
		{
			for (uint index = 1; index < argumentCount; index++)
			{
				if (index > 1)
				{
					Console.Write(" ");
				}

				Console.Write(arguments[index].ConvertToString().ToString());
			}

			Console.WriteLine();

			return JavaScriptValue.Invalid;
		}

		private static JavaScriptValue RunScript(JavaScriptValue callee, bool isConstructCall, JavaScriptValue[] arguments, ushort argumentCount, IntPtr callbackData)
		{
			if (argumentCount < 2)
			{
				ThrowException("not enough arguments");
				return JavaScriptValue.Invalid;
			}

			//
			// Convert filename.
			//

			string filename = arguments[1].ToString();

			//
			// Load the script from the disk.
			//

			string script = File.ReadAllText(filename);
			if (string.IsNullOrEmpty(script))
			{
				ThrowException("invalid script");
				return JavaScriptValue.Invalid;
			}

			//
			// Run the script.
			//

			return JavaScriptContext.RunScript(script, currentSourceContext++, filename);
		}

		private static void DefineHostCallback(JavaScriptValue globalObject, string callbackName, JavaScriptNativeFunction callback, IntPtr callbackData)
		{
			//
			// Get property ID.
			//

			JavaScriptPropertyId propertyId = JavaScriptPropertyId.FromString(callbackName);

			//
			// Create a function
			//

			JavaScriptValue function = JavaScriptValue.CreateFunction(callback, callbackData);

			//
			// Set the property
			//

			globalObject.SetProperty(propertyId, function, true);
		}
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);

		}
		private bool disposed = false;
		protected virtual void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				if (disposing)
				{
					runtime.Dispose();
				}
			}
			this.disposed = true;
		}

	}

}
