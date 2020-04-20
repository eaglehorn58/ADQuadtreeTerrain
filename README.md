# ADQuadtreeTerrain

Copyright(c) 2020, Andy Do.

License type: MIT

Contact: eaglehorn58@gmail.com, eaglehorn58@163.com

---------------------------------------------
The project was made by Unity2019.3.9f1 on Windows10.

A large-scale terrain example based on Quadtree algorithm. The example generates a 4K x 4K terrain from a raw 16-bit heightmap which can be made by terrain tools like World Machine. Bigger scale terrains can be supported without difficulty, except that more memory is required. Benefit from the Quadtree the view distance can be set to very high (4K for example) without losing much perfermance. In the code Graphics.DrawProcedural method is used to do render work. Vertex and index data are stored in ComputeBuffers, no GameObject, Mesh or MeshRenderer are created, so it's also a example for how to draw custom meshes in Unity3D.

There is still a long way to go to make the little example become a completed terrain system that can be used in games, the future work may includes terrain editor, multithread data streaming, material system, collision check, etc. But if you can get some ideas or inspirations from this example, I will be very pleased.

The way to use the package:
1. Import the package into Unity3D.
2. Open the scene: Assets/ADQuadtreeTerrain/Scenes/ADQuadtreeTerrain.unity.
3. Play the game.
4. Use WASDQE keys and right button to control camera.
5. Play with the parameters on the inspector panels of QTreeTerrain and MainCamera.

![image](https://github.com/eaglehorn58/ADQuadtreeTerrain/tree/master/Images/QuadtreeTerrain03.jpg)
![image](https://github.com/eaglehorn58/ADQuadtreeTerrain/tree/master/Images/QuadtreeTerrain04.jpg)
![image](https://github.com/eaglehorn58/ADQuadtreeTerrain/tree/master/Images/QuadtreeTerrain05.jpg)
![image](https://github.com/eaglehorn58/ADQuadtreeTerrain/tree/master/Images/QuadtreeTerrain06.jpg)

