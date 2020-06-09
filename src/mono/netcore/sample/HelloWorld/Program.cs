using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(3.13.ToString(CultureInfo.GetCultureInfo("ru-RU")));
            Console.WriteLine(CultureInfo.GetCultureInfo("ru-RU").NativeName);
        }
    }
}