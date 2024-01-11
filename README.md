![alt tag](https://github.com/jchristn/PQueue/blob/main/src/PQueue/Assets/icon.png?raw=true)

# PQueue

[![NuGet Version](https://img.shields.io/nuget/v/PQueue.svg?style=flat)](https://www.nuget.org/packages/PQueue/) [![NuGet](https://img.shields.io/nuget/dt/PQueue.svg)](https://www.nuget.org/packages/PQueue) 

Lightweight, persistent, thread-safe, disk-based queue written in C#. 

## New in v1.0.x

- Initial release
- Added expiration to queued data

## Getting Started

Refer to the ```Test``` project for a working example.

```csharp
using PQueue;

PersistentQueue queue = null;
queue = new PersistentQueue("./temp/");        // persist data even after disposed
queue = new PersistentQueue("./temp/", true);  // delete data after disposed

string key = null;

// Add to the queue...
key = queue.Enqueue("Hello, world!");            
key = await queue.EnqueueAsync("Hello, world!"); // async

// Add to the queue with expiration...
key = queue.Enqueue("Hello, world!", DateTime.Parse("1/10/2024 01:23:45"));              
key = await queue.EnqueueAsync("Hello, world!", DateTime.Parse("1/10/2024 01:23:45")); // async

(string, byte[])? data = null;
data = queue.Dequeue();                // get the latest
data = queue.Dequeue(key);             // get a specific entry
data = queue.Dequeue(key, true);       // get a specific entry and delete it
data = await.queue.DequeueAsync(key);  // get a specific entry asynchronously

if (data != null) 
  Console.WriteLine(data.Item1 + ": " + Encoding.UTF8.GetString(data.Item2));

queue.Purge(key);  // delete a specific entry
queue.Expire(key); // expire a specific entry

DateTime? expiry = queue.GetExpiration(key); // get the expiration time of a specific entry

Console.WriteLine("Queue depth  : " + queue.Depth);
Console.WriteLine("Queue length : " + queue.Length + " bytes");
```

## Version History

Refer to CHANGELOG.md for version history.
