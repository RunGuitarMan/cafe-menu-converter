using System.Text.Json;
using System.Text.Encodings.Web;
using CafeMenuConverter.Core.Pdf;

object result = args.Contains("--words") ? PdfTextReader.Read(args[0]) : new CafeMenuConverter.Core.MenuConverter().Convert(args[0]);
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
