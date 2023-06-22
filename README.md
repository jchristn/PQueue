![alt tag](https://github.com/jchristn/PQueue/blob/main/src/PQueue/Assets/icon.png?raw=true)

# PQueue

[![NuGet Version](https://img.shields.io/nuget/v/PQueue.svg?style=flat)](https://www.nuget.org/packages/PQueue/) [![NuGet](https://img.shields.io/nuget/dt/PQueue.svg)](https://www.nuget.org/packages/PQueue) 

Lightweight, persistent, thread-safe, disk-based queue written in C#. 

## New in v1.0.x

- Initial release

## Getting Started

Refer to the ```Test``` project for a working example.

```csharp
using PQueue;

PersistentQueue queue = null;
queue = new PersistentQueue("./temp/");        // persist data even after disposed
queue = new PersistentQueue("./temp/", true);  // delete data after disposed

string key = null;
key = queue.Enqueue(Encoding.UTF8.GetBytes("Hello, world!"));             // add to the queue
key = await queue.EnqueueAsync(Encoding.UTF8.GetBytes("Hello, world!"));  // add to the queue asynchronously

byte[] data = null;
data = queue.Dequeue();                // get the latest
data = queue.Dequeue(key);             // get a specific entry
data = queue.Dequeue(key, true);       // get a specific entry and delete it
data = await.queue.DequeueAsync(key);  // get a specific entry asynchronously

queue.Purge(key);  // delete a specific entry

Console.WriteLine("Queue depth: " + queue.Depth);
```

## Version History

Refer to CHANGELOG.md for version history.
