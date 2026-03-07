using BenchmarkDotNet.Running;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
