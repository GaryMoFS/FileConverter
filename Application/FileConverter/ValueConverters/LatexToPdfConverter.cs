using System.Diagnostics;
using System.IO;
using FileConverter.ValueConverters;

namespace FileConverter.ValueConverters
{
    public sealed class LatexToPdfConverter : ValueConverterBase
    {
        public override string[] InputExtensions => new[] { ".tex" };
        public override string[] OutputExtensions => new[] { ".pdf" };
        public override string DisplayName => "LaTeX to PDF";
        public override string HelpText => "Converts LaTeX files to PDF using pdflatex";

        public override bool Convert(string inputPath, string outputPath, ValueConverterContext context)
        {
            var workingDir = Path.GetDirectoryName(inputPath);
            var fileName = Path.GetFileName(inputPath);
            var baseName = Path.GetFileNameWithoutExtension(inputPath);

            try
            {
                // First pdflatex run
                if (!RunProcess("pdflatex", $"-interaction=nonstopmode \"{fileName}\"", workingDir, context))
                    return false;

                // Check for citations
                var auxPath = Path.Combine(workingDir, $"{baseName}.aux");
                if (File.Exists(auxPath))
                {
                    // Run BibTeX if citations exist
                    if (File.ReadAllText(auxPath).Contains("\\citation"))
                    {
                        if (!RunProcess("bibtex", baseName, workingDir, context))
                            return false;
                        
                        // Two more pdflatex runs after BibTeX
                        if (!RunProcess("pdflatex", $"-interaction=nonstopmode \"{fileName}\"", workingDir, context))
                            return false;
                    }
                }

                // Final pdflatex run
                if (!RunProcess("pdflatex", $"-interaction=nonstopmode \"{fileName}\"", workingDir, context))
                    return false;

                // Move resulting PDF
                var pdfPath = Path.Combine(workingDir, $"{baseName}.pdf");
                if (File.Exists(pdfPath))
                {
                    File.Move(pdfPath, outputPath, true);
                    return true;
                }
                
                context.ReportError("No PDF output was generated");
                return false;
            }
            catch (Exception ex)
            {
                context.ReportError($"Conversion failed: {ex.Message}");
                return false;
            }
        }

        private bool RunProcess(string command, string args, string workingDir, ValueConverterContext context)
        {
            try
            {
                context.ReportProgress($"Running {command}...");
                
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = args,
                        WorkingDirectory = workingDir,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                if (!string.IsNullOrWhiteSpace(output))
                    context.ReportProgress(output);
                
                if (process.ExitCode != 0)
                {
                    context.ReportError($"{command} failed: {error}");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                context.ReportError($"Failed to run {command}: {ex.Message}");
                return false;
            }
        }
    }
}
