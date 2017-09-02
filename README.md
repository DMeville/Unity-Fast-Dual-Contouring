This code is half broken!

I've been playing around with generating meshes from noise data for about the last week, after trying multiple unity assets and not being satisfied with the results, thought I'd see if I could get DC working quickly.  I'm hoping to be able to use this as a base to generate procedural terrains.  I don't require the terrains to be modifiable at runtime, but I would like to be able to swap between mesh LoD at will.

Anyways, after reading the paper, and taking a look at the C# port of nickgildeas code here https://github.com/tuckbone/DualContouringCSharp helped me understand.  I tried to implement DC using a normal voxel grid instead of an octree and it worked decently well, but it was dreadfully slow.  Decided it would be fun to see about getting it to run as a C++ plugin..

Instead of porting my own code I grabbed [nickgildea's code from github](https://github.com/nickgildea/fast_dual_contouring) and after a bit of tinkering and trying to remember how C++ works, I got it working as a plugin in Unity!

Still has  a few things I need to figure out like if I'm leaking memory the way I'm sending the generated mesh data to Unity, and why the mesh simplification doesn't seem to be working, but I'm not familiar with C++ much at all so I'm sort of stumbling around..


Feel free to suggest changes or fixes to the code, I don't know how to C++ well so it's probably terrible!

Video of it in action: https://www.youtube.com/watch?v=fAXttypzvIw

@DMeville
I have no idea what I'm doing.
