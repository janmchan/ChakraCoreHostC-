using System;
using System.IO;
using ChakraHost.Hosting;
using ChakraCoreHost;

namespace ChakraHost
{
    public static class Program
    {
        
        //
        // The main entry point for the host.
        //
        public static int Main(string[] arguments)
        {
			using (var instance = new Zxcvbn())
			{
				var zxcvbn = instance.GetZxcvbn("test12345!");

				Console.WriteLine("Score: " + zxcvbn.score);
				Console.WriteLine("Warning	: " + zxcvbn.feedback.warning);
				Console.ReadLine();
			}
				
			return 1;

		}
    }
}