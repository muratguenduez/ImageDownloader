// See https://aka.ms/new-console-template for more information
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;


int count = 0;
int parallelism = 0;
string savePath = "";

// // Check if the input file exists
if (File.Exists("./Input.json"))
{
    Console.WriteLine("Input file has already exist, then will continue with the file");
    string inputFile = File.ReadAllText("./Input.json");
    JsonConfig config = JsonConvert.DeserializeObject<JsonConfig>(inputFile);

    count = config.Count;
    parallelism = config.Parallelism;
    savePath = config.SavePath;
}
else
{
    Console.WriteLine("Enter the number of images to download:");
    count = int.Parse(Console.ReadLine());

    Console.WriteLine("Enter the maximum parallel download limit:");
    parallelism = int.Parse(Console.ReadLine());

    Console.WriteLine("Enter the save path (default: ./outputs):");
    savePath = Console.ReadLine();

    if (string.IsNullOrEmpty(savePath))
        savePath = "./outputs";

}

// Check if the folder exists
if (!Directory.Exists(savePath))
{
    Console.WriteLine("Folder doesnt exist...");

    // Create the folder
    Directory.CreateDirectory(savePath);
    Console.WriteLine("Folder created successfully.");
}

Console.WriteLine($"Downloading {count} images ({parallelism} parallel downloads at most)");

// Create a cancellation token source to allow cancellation of the download process
CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

// Create a list to store the downloaded image paths
List<string> downloadedImagePaths = new List<string>();

// Create a semaphore to control the parallel downloads
SemaphoreSlim semaphore = new SemaphoreSlim(parallelism);

// Create a progress counter
int progress = 0;

// Event to track progress updates
EventHandler<int> progressUpdated = (sender, currentProgress) =>
{
    Console.SetCursorPosition(0, Console.CursorTop);
    Console.Write($" Progress: {currentProgress}/{count}");
};

// Start the progress tracking
progressUpdated.Invoke(null, progress);

try
{
    // Start the image downloading tasks
    List<Task> downloadTasks = new List<Task>();
    for (int i = 0; i < count; i++)
    {
        await semaphore.WaitAsync(cancellationTokenSource.Token);

        downloadTasks.Add(Task.Run(async () =>
        {
            try
            {
                // Image URL
                string imageUrl = $"https://picsum.photos/{200}/{300}";

                // Download the image
                using (WebClient client = new WebClient())
                {
                    string imagePath = Path.Combine(savePath, $"{i}.png");
                    await client.DownloadFileTaskAsync(imageUrl, imagePath);
                    downloadedImagePaths.Add(imagePath);
                }

                Interlocked.Increment(ref progress);
                progressUpdated.Invoke(null, progress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error downloading image: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }, cancellationTokenSource.Token));
    }

    // Wait for all download tasks to complete
    await Task.WhenAll(downloadTasks);
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nImage download process cancelled.");
}
finally
{
    // Clean up downloaded images if the process was cancelled
    if (cancellationTokenSource.IsCancellationRequested)
    {
        foreach (string imagePath in downloadedImagePaths)
        {
            File.Delete(imagePath);
        }
    }
}

Console.WriteLine($"\nAll images downloaded and saved to {savePath}.");

class JsonConfig
{
    public int Count { get; set; }
    public int Parallelism { get; set; }
    public string SavePath { get; set; }
}

