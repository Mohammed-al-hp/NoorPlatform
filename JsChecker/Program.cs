using System;
using System.IO;
using Esprima;

class Program
{
    static void Main()
    {
        string jsCode = File.ReadAllText("../NoorPlatform.Api/wwwroot/inline-script.js");
        try
        {
            var parser = new JavaScriptParser();
            parser.ParseScript(jsCode);
            Console.WriteLine("No syntax errors found!");
        }
        catch (ParserException ex)
        {
            Console.WriteLine($"Syntax Error: {ex.Message} at line {ex.LineNumber}, column {ex.Column}");
        }
    }
}
