using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System;
using UnityEngine;
using UnityEditor;
using Unity.EditorCoroutines.Editor;
using System.Threading;
using Timer = System.Timers.Timer;
using System.Timers;
using BuildABot;
using Newtonsoft.Json;

[InitializeOnLoad]
public class PrefabGenerator : MonoBehaviour
{
  private static FileSystemWatcher watcher;
  private static List<string> pathList = new List<string>();
  private static ReaderWriterLockSlim pathListLock = new ReaderWriterLockSlim();
  private static Timer pathListTimer;

  static PrefabGenerator()
  {
    // Watch for new files
    watcher = new FileSystemWatcher(@"Assets/Models")
    {
      NotifyFilter = NotifyFilters.CreationTime
                         | NotifyFilters.FileName
                         | NotifyFilters.LastWrite,

      Filter = "*.fbx"
    };
    watcher.Created += OnCreated;
    watcher.IncludeSubdirectories = true;
    watcher.EnableRaisingEvents = true;
  }

  private static void OnCreated(object sender, FileSystemEventArgs e)
  {
    if (e.ChangeType != WatcherChangeTypes.Created)
    {
      return;
    }

    pathListLock.EnterWriteLock();

    try
    {
      // Queue up file path
      pathList.Add(e.FullPath);
      if (pathListTimer == null) // First file, set timer
      {
        pathListTimer = new Timer(2000); // Set timer
        pathListTimer.Elapsed += RunPrefabCreation; // Run prefab creation when timer ends
        pathListTimer.Start(); // Start timer
      }
      else // Another file, restart the timer
      {
        pathListTimer.Stop();
        pathListTimer.Start();
      }
    }
    finally
    {
      pathListLock.ExitWriteLock();
    }
  }

  private static void RunPrefabCreation(object sender, ElapsedEventArgs e)
  {
    pathListLock.EnterReadLock();
    try
    {
      foreach (string s in pathList)
      {
        EditorCoroutineUtility.StartCoroutineOwnerless(CreatePrefab(s)); // Create each prefab
      }
      pathList.Clear();
    }
    finally
    {
      if (pathListTimer != null) // Reset the timer for the next batch of files
      {
        pathListTimer.Stop();
        pathListTimer.Dispose();
        pathListTimer = null;
      }
      pathListLock.ExitReadLock();
    }
  }

  private static IEnumerator CreatePrefab(string path)
  {
    yield return new EditorWaitForSeconds(0);

    Match mName = Regex.Match(path, @"([A-Za-z]+)_([A-Za-z]+)_(\d+)\.fbx");
    Match mLoadPath = Regex.Match(path, @"\\Models([\\\/\w]+).fbx");
    string name = mName.Groups[1].Value + "_" + mName.Groups[2].Value + "_" + mName.Groups[3].Value;

    string savePath = "Assets/Resources/Prefabs/" + name + ".prefab";
    if (AssetDatabase.FindAssets(savePath).Length != 0) yield break;
    savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

    GameObject loaded = (GameObject)AssetDatabase.LoadAssetAtPath("Assets/" + mLoadPath.Value, typeof(GameObject));
    if (loaded == null)
    {
      Debug.LogError("Something went wrong :( Most likely, you need to make sure your model is set up properly.");
      yield break;
    }


    GameObject start = Instantiate(loaded);
    if (start == null)
    {
      Debug.LogError("Something went wrong :( Most likely, you need to make sure your model is set up properly.");

      DestroyImmediate(loaded);
      yield break;
    }

    start.name = name;

    // Create MeshCollider
    MeshCollider meshCollider = start.AddComponent<MeshCollider>();
    meshCollider.sharedMesh = start.GetComponent<MeshFilter>().sharedMesh;
    meshCollider.convex = true;
    meshCollider.isTrigger = false;

    // Create RigidBody
    Rigidbody rigidBody = start.AddComponent<Rigidbody>();
    rigidBody.useGravity = false;
    rigidBody.isKinematic = true;

    Drag dragScript = start.AddComponent<Drag>();

    Outline outline = start.AddComponent<Outline>();

    PartController partController = start.AddComponent<PartController>();

    start.tag = "part";

    Texture2D icon = AssetPreview.GetAssetPreview(start);

    byte[] iconBytes = icon.EncodeToPNG();

    File.WriteAllBytes(Application.dataPath + "/Resources/ModelIcons/" + start.name + ".png", iconBytes);

    // Create prefab
    GameObject created = PrefabUtility.SaveAsPrefabAssetAndConnect(start, savePath, InteractionMode.UserAction);

    DestroyImmediate(start);
    DestroyImmediate(icon);

    Debug.Log("Prefab created at " + savePath);
  }
}
