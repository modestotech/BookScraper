# BookScraper
This repo is a solution to the tretton37 assignment.
I've solved it with a console application in .NET/C#. The version used is the latest LTS (.NET8).

## Introduction
In the base commit, I've setup an editorconfig file, a gitignore file, and added the StyleCop nuget package to the solution build props, so that all projects that might be created in the solution will get that nuget.

## How to use
Just run through console or Visual Studio debugger. The folder containing the site is created inside the project bin folder so not needed to run with elevated permissions.

## Flowchart
```mermaid
flowchart TD
    A[Start Program] --> A1{Is there an existing folder?}
    A1 -- No --> A2[Create base folder]
    A1 -- Yes --> A3[Clean base folder]

    B[Enqueue Base URL]
    A2 --> B
    A3 --> B

    B --> C[Mark Base URL as Processed]
    C --> D{Is Queue Empty?}
    D -- No --> E[Dequeue URL]
    E --> F[Wait for Semaphore Slot]
    F --> G[Download Page Content]
    G --> H[Save Page to Disk]
    H --> I[Extract Links from Page]

    I --> J{Any links on page?}
    J -- Yes --> K[[For Each New Link]]
    K --> L{Has Link Been Processed Before?}
    L -- No --> M[Enqueue New Link]
    L -- Yes --> N[Skip Link]
    M --> N
    N --> O[Release Semaphore Slot]
    J -- No --> O
    O --> D
    D -- Yes --> P[Wait for All Tasks to Finish]
    P --> Q[End Program]

    subgraph LinkProcessing[ ]
        K
        L
        M
        N
    end
```