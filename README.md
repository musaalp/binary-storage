# Binary Storage
Its a sample console application build using .Net Core 3.0. The architecture and design of the project is explained below.


# Introduction
Binary Storage is a write once read many data structure stored on the hard drive. It should provide persistent storage for arbitrary binary content (stream of bytes). When adding new data to the storage client provides a string key that will be associated with this data. Once added to the storage the data cannot be modified through storage API. After data has been successfully added to the storage client could request it for reading by key and it should be returned as a stream of bytes. The storage should be thread-safe and should support multi-threaded addition of new data and reading of existing data. Logical organization of the Binary Storage is presented on the picture below:

![Class Diagram](https://github.com/musaalp/BinaryStorage/blob/master/assets/storage.png)


# Getting Started
Use these instructions to get the project up and running.


## Prerequisites
You will need the following tools:

* [Visual Studio Code or Visual Studio 2019](https://visualstudio.microsoft.com/vs/) (version 16.3 or later)
* [.NET Core SDK 3](https://dotnet.microsoft.com/download/dotnet-core/3.0)


Following nuget packages used in the project:
* Microsoft.NET.Test.Sdk 16.2.0
* MSTest.TestAdapter 2.0.0
* MSTest.TestFramework 2.0.0
* coverlet.collector 1.0.1


## Setup
Follow these steps to get your development environment set up:

* At the `/BinaryStorage/src/BinStorage` directory, restore required packages by running:
 
     ```
     dotnet restore
     ```
	 
* Next, build the solution by running:
 
     ```
     dotnet build
     ```	 
	 
* Launch the TestApp with providing InputFolder and StorageFolder path by running:
 
     ```
     dotnet src\BinStorage.TestApp\bin\Debug\netcoreapp3.0\BinStorage.TestApp.dll C:\Folders\InputFolder C:\Folders\StorageFolder
     ```	 
	 
* In following example InputFolder is: `C:\Folders\InputFolder`, and StorageFolder is: `C:\Folders\StorageFolder`. Make sure both folder are exists


## Run Unit Tests
* At the `/BinaryStorage/src/BinStorage.Test` directory, execute line code below to run unit tests:
 
     ```
     dotnet test
     ```


## Sequence Diagram
Sequence diagram basically shows how application works.

![Sequence Diagram](https://github.com/musaalp/BinaryStorage/blob/master/assets/sequence-diagram.png)


## Descriptions

The solution consists of two main parts: Index and File Storage. Interaction those two parts is encapsulated in `BinaryStorage` class.

Index is implemented using Dictionary data structure in `PersistenIndex` class. Dictionary consist of key value pairs. Key is original key, value is a Node. This structure is chosen since it is easy to implement and is ideally suited to the task.

Thread-safety of the index is achieved with wrapper class `ThreadSafeIndex`, which is implemented using `ReaderWriterLockSlim`. `ThreadSafeIndex` is an implementation of Decorator pattern.

To support large index `PersistentIndex` is created, which stores index data on the disk.

File Storage is responsible to append incoming stream to persistent file on the disk via `MemoryMappedFile`.
`PersistentIndex` keeps offset and size of the data on the Storage. It uses RootNode object to perform add and read transactions.

While transactions perform, if any error occurs rollback operation is performed. Saving data to the disk is implemented in the Commit method.

Structure of Storage and Index on the disc is shown below

![Class Diagram](https://github.com/musaalp/BinaryStorage/blob/master/assets/storage-bin.png)
**Default Storage.bin size is 1Gb. Its configurable from FileStorage constructor method.**


First 8 byte of Storage.bin keeps cursor information. The rest part of stream is a collection of data. Each data represents with offset and size.

![Class Diagram](https://github.com/musaalp/BinaryStorage/blob/master/assets/index-bin.png)
**Default Index.bin size is 16Mb. Its configurable from PersistentIndex constructor method.**


![Class Diagram](https://github.com/musaalp/BinaryStorage/blob/master/assets/index-data.png)

First 8 byte of Index.bin keeps cursor information. Rest part of stream consist of RootNode information. 
RootNode consist of collection of Node. Each Node represents key information on the index.bin and references of data on storage.bin with IndexData object.

IndexData object, keeps original data references. Offset is a starting point of original data on storage.bin, Size is an end point of the original file on storage.bin. Md5 hash byte array – hash of stored data.

If you try to add a duplicate key to the index, DuplicateException is thrown.


Data storage logic is implemented in the FileStorage class. To read and write data to the storage used MemoryMappedFile. This class is used because it supports multi-threading read and write out of the box. Also, this class takes over memory management, which makes life easier. The only drawback – the class is not able to dynamically expand its capacity, and therefore, when the limit is reached, it is necessary to reinitialize it with new capacity. To reduce the number of reinitialization, size of the file is increased twice when the limit is reached. The corresponding logic is implemented in the EnsureCapacity method. Thread safe of this operation is ensured by ReaderWriterLockSlim. When reinitialization happens no one can read or write to the storage. It is the only bottleneck in this solution. To mitigate it, the initial storage size is set to 1 GB.

In the file storage, first 8 bytes are reserved for cursor position in the file, because file size is not reflect size stored data. If disk is full, NotEnoughDiskSpaceException exception will be thrown.

Next fields have been added to the class StorageConfiguration:
* **StorageFileName**: Name for storage file.
* **IndexFileName**: Name for index file.
* **IndexTimeout**: Index lock timeout. It is used in ThreadSafeIndex.


## Measurements
Testing was done on a system.
* RAM: 12GB.
* Processor: Intel(R) Core(TM) i5-2500K CPU @ 3.30GHz
* Drive: SSD KINGSTON SV300S37A 120G

Application has been tested against 25000 partial files and total size was more than 3GB. 

**Original storage capacity: 1 GB**

**4KB Read Buffer**
| 4 Threads Create| 4 Threads Verify| 1 Threads Create| 1 Threads Verify|
| --- | --- | --- | --- |
| 00:01:02.6208675 | 00:00:08.3134765 | 00:01:08.3896127 | 00:00:25.5119852 |
| 00:00:54.8807781 | 00:00:08.0884549 | 00:01:08.9440978 | 00:00:25.0673080 |
| 00:01:00.3516628 | 00:00:08.6632553 | 00:01:11.7473738 | 00:00:25.1777349 |

**16KB Read Buffer**
| 4 Threads Create| 4 Threads Verify| 1 Threads Create| 1 Threads Verify|
| --- | --- | --- | --- |
| 00:00:53.2379449 | 00:00:14.2317433 | 00:00:51.7422517 | 00:00:25.1328837 |
| 00:00:57.1192474 | 00:00:09.7239829 | 00:00:51.6723660 | 00:00:25.2803442 |
| 00:00:53.0685755 | 00:00:09.9333049 | 00:00:51.8628035 | 00:00:25.4939600 |


**Original storage capacity: 5 GB**

**16KB Read Buffer**
| 4 Threads Create| 4 Threads Verify| 1 Threads Create| 1 Threads Verify|
| --- | --- | --- | --- |
| 00:00:52.5724000 | 00:00:08.3290930 | 00:01:01.2202944 | 00:00:25.9159960 |
| 00:00:50.3514333 | 00:00:10.6075779 | 00:00:52.3783722 | 00:00:26.1815756 |


Testing shows that:

* Reinitialization capacity of MemoryMappedFile in FileStorage class does not affect performance much.
* Best performance is achieved with 16 KB read buffer with multi threading.
* Multithreaded running does not affect performance much in average.
* Multithreaded running is 149%  performance growth for reading data with 16KB in average. 
