using System.Diagnostics;
using System.Text;
using REST;


// A simple example that updates some shared resource, caches it as a response and returns it when queried through the REST API

object locker = new object();
var cancellationTokenSource = new CancellationTokenSource();
Response? sharedResponse = null;
var notFound = new Response
{
	Code = System.Net.HttpStatusCode.NotFound,
	Body = "The resource could not be found, please try again"
};

Task.Run(SharedDataUpdater);

async Task SharedDataUpdater()
{
	var stopwatch = new Stopwatch();
	stopwatch.Start();
	var builder = new StringBuilder();
	while (!cancellationTokenSource.IsCancellationRequested)
	{
		stopwatch.Stop();
		var sinValue = Math.Sin(stopwatch.Elapsed.TotalSeconds * 0.3);
		stopwatch.Start();
		builder.AppendLine($"time={DateTime.UtcNow.Ticks}");
		builder.AppendLine($"value={sinValue}");
		builder.Append($"check={(Random.Shared.NextSingle() > 0.5f ? "1" : "0")}");
		sharedResponse = new Response
		{
			Body = builder.ToString()
		};
		builder.Clear();

		await Task.Delay(100, cancellationTokenSource.Token);
	}
}

async Task<Response> GetSharedData(Request request) => sharedResponse ?? notFound;



// Define and run server
var server = new Server(11311)
	// Configure
	.SetLocalOnly(true)

	// Add endpoints
	.Get("api/hello-world", async request => new Response { Body = "Hello World!" })
	.Get("api/body", async request => new Response { Body = request.Body })
	.Get("api/shared", GetSharedData)

	// Start and wait until the server exits
	.Start()
	;


// Wait for ESC press
while(Console.ReadKey(true).Key != ConsoleKey.Escape) { }

// Stop everything
cancellationTokenSource.Cancel();
server.Stop();