# RoslynCompileAndRun

This code is an example of how to compile, link, and run C# code with Roslyn under either
Net Core or Net Framework. It works, but it finding the solution was one of the most
painful diversions I did this month because THE DOCUMENTATION FOR ROSLYN SUCKS!

The problem is simple:

* Create two programs, one in Net Core, the other in Net Framework.
* Create a Net Standard library.
* In the program, create C# code in a string that makes a call to the Net Standard library created
 above. Set up a Roslyn workspace/project/document
 that will compile the code into an assembly. This second assembly is a library that references
 the code in the above assembly, plus references the libraries used by that library.
* Once it compiles and links, load the damn thing and run it.

Sounds easy, huh? Not!

The key is figuring out what to pull in of the referenced assemblies. Read the code.

Also, you might want to look at the following pages, which were only slightly helpful. The only
solution was to use brute force trial and error. The docs suck.

* https://github.com/dotnet/roslyn/wiki/Runtime-code-generation-using-Roslyn-compilations-in-.NET-Core-App
* https://github.com/dotnet/roslyn/issues/21205
* https://luisfsgoncalves.wordpress.com/2017/03/20/referencing-system-assemblies-in-roslyn-compilations/
* https://www.codeproject.com/Articles/1215168/Using-Roslyn-for-Compiling-Code-into-Separate-Net

--Ken, December 27, 2018
