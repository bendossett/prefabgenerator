# prefabgenerator
Basic Prefab Generation for Unity

I wrote this for a project I was working on. Not sure if it will be helpful for others in its current state, I might modify it to make it more re-useable for different projects.

Currently, it watches a folder for new files (in our case, these were .fbx 3d model files). When files are added, it creates Unity prefabs for each of the models.

Credit to this stackoverflow answer for the path processing code (using the timer and locks, etc.): https://stackoverflow.com/a/6944516
