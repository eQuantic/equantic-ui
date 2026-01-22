using System.Diagnostics;

namespace eQuantic.UI.CLI.Commands;

public static class DevCommand
{
    public static void Execute(int port, string input)
    {
        Console.WriteLine("🚀 eQuantic.UI Development Server");
        Console.WriteLine($"   Port:  {port}");
        Console.WriteLine($"   Input: {Path.GetFullPath(input)}");
        Console.WriteLine();
        
        // Create temp output directory
        var tempOutput = Path.Combine(Path.GetTempPath(), "eqx-dev", Guid.NewGuid().ToString()[..8]);
        Directory.CreateDirectory(tempOutput);
        
        // Initial build
        Console.WriteLine("⚙️  Initial compilation...");
        BuildCommand.Execute(input, tempOutput, watch: false);
        Console.WriteLine();
        
        // Generate index.html
        GenerateDevHtml(tempOutput, port);
        
        // Start a simple HTTP server (using Python for now, or can use Kestrel)
        Console.WriteLine($"🌐 Starting server at http://localhost:{port}");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"   → Open http://localhost:{port} in your browser");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("👀 Watching for changes... (Ctrl+C to stop)");
        Console.WriteLine();
        
        // Try to start Python HTTP server
        StartHttpServer(tempOutput, port);
    }
    
    private static void GenerateDevHtml(string outputDir, int port)
    {
        var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>eQuantic.UI Dev</title>
    <style>
        :root {{
            --color-primary: #3b82f6;
            --color-primary-light: #60a5fa;
            --color-primary-dark: #2563eb;
        }}
        
        * {{
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }}
        
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
        }}
        
        #app {{
            background: white;
            border-radius: 16px;
            padding: 32px;
            box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25);
            min-width: 400px;
        }}
        
        .title {{
            color: #1f2937;
            margin-bottom: 24px;
            text-align: center;
        }}
        
        .counter {{
            text-align: center;
        }}
        
        input {{
            width: 100%;
            padding: 12px 16px;
            border: 2px solid #e5e7eb;
            border-radius: 8px;
            font-size: 16px;
            margin-bottom: 16px;
            transition: border-color 0.2s;
        }}
        
        input:focus {{
            outline: none;
            border-color: var(--color-primary);
        }}
        
        .btn {{
            padding: 12px 24px;
            border: none;
            border-radius: 8px;
            font-size: 18px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.2s;
        }}
        
        .btn-primary {{
            background: var(--color-primary);
            color: white;
        }}
        
        .btn-primary:hover {{
            background: var(--color-primary-dark);
            transform: translateY(-1px);
        }}
        
        .btn-secondary {{
            background: #e5e7eb;
            color: #374151;
        }}
        
        .btn-secondary:hover {{
            background: #d1d5db;
        }}
        
        .count-display {{
            font-size: 48px;
            font-weight: bold;
            color: #1f2937;
            margin: 0 24px;
        }}
        
        .message-display {{
            color: #6b7280;
            margin-top: 16px;
        }}
    </style>
</head>
<body>
    <div id=""app"">
        <h1 class=""title"">eQuantic.UI Counter</h1>
        <div class=""counter"">
            <input id=""message-input"" type=""text"" placeholder=""Type something..."" />
            <div style=""display: flex; gap: 8px; justify-content: center; align-items: center;"">
                <button id=""decrement-btn"" class=""btn btn-secondary"">-</button>
                <span id=""count-display"" class=""count-display"">0</span>
                <button id=""increment-btn"" class=""btn btn-primary"">+</button>
            </div>
            <p id=""message-display"" class=""message-display"" style=""display: none;""></p>
        </div>
    </div>
    
    <script>
        // Simple counter demo (will be replaced by compiled JS)
        let count = 0;
        let message = '';
        
        const countDisplay = document.getElementById('count-display');
        const messageDisplay = document.getElementById('message-display');
        const messageInput = document.getElementById('message-input');
        
        document.getElementById('increment-btn').addEventListener('click', () => {{
            count++;
            render();
        }});
        
        document.getElementById('decrement-btn').addEventListener('click', () => {{
            count--;
            render();
        }});
        
        messageInput.addEventListener('input', (e) => {{
            message = e.target.value;
            render();
        }});
        
        function render() {{
            countDisplay.textContent = count;
            if (count > 0 && message) {{
                messageDisplay.textContent = `Message: ${{message}}`;
                messageDisplay.style.display = 'block';
            }} else {{
                messageDisplay.style.display = 'none';
            }}
        }}
    </script>
</body>
</html>";

        File.WriteAllText(Path.Combine(outputDir, "index.html"), html);
    }
    
    private static void StartHttpServer(string directory, int port)
    {
        try
        {
            // Try Python first
            var pythonProcess = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"-m http.server {port}",
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            var process = Process.Start(pythonProcess);
            
            if (process != null)
            {
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    process.Kill();
                };
                
                process.WaitForExit();
            }
        }
        catch
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  Python not found. Please start a web server manually:");
            Console.WriteLine($"   cd {directory}");
            Console.WriteLine($"   npx serve -p {port}");
            Console.ResetColor();
            
            // Keep running for file watching
            new ManualResetEvent(false).WaitOne();
        }
    }
}
