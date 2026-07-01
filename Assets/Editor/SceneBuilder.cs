using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneBuilder
{
    private const string FbxPath = "Assets/Models/Dinos.fbx";
    private const string ScenePath = "Assets/Scenes/DinoIdle.unity";

    public static void BuildDinoScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var model = LoadAndInstantiateModel();
        AddGround();
        AddLighting();
        AddCamera(model);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();

        Debug.Log("DinoIdle scene built and saved to " + ScenePath);
    }

    private static GameObject LoadAndInstantiateModel()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (prefab == null)
        {
            Debug.LogError("Could not load FBX at " + FbxPath);
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "Dinos";
        instance.transform.position = Vector3.zero;

        var collider = instance.AddComponent<SphereCollider>();
        collider.center = new Vector3(0f, 0.5f, 0f);
        collider.radius = 0.6f;

        instance.AddComponent<DinoIdleController>();

        return instance;
    }

    private static Material MakeUnlitColor(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader) { name = name };
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, "Assets/Materials/" + name + ".mat");
        return mat;
    }

    private static void AddGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(2f, 1f, 2f);

        var mat = MakeUnlitColor("GroundPastel", new Color(0.95f, 0.90f, 0.85f));
        ground.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static void AddLighting()
    {
        // Key light: bright, from front-upper-side
        var key = new GameObject("KeyLight").AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.98f, 0.92f);
        key.intensity = 1.1f;
        key.shadows = LightShadows.Soft;
        key.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        // Fill light: softer, opposite side, no shadows
        var fill = new GameObject("FillLight").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.85f, 0.9f, 1f);
        fill.intensity = 0.45f;
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

        // Rim light: from behind, gives an edge highlight
        var rim = new GameObject("RimLight").AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.color = Color.white;
        rim.intensity = 0.6f;
        rim.shadows = LightShadows.None;
        rim.transform.rotation = Quaternion.Euler(20f, 180f, 0f);
    }

    private static void AddCamera(GameObject model)
    {
        var camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        var cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.86f, 0.93f, 0.98f);
        cam.fieldOfView = 35f;

        // Frame the character in a mobile portrait aspect
        camObj.transform.position = new Vector3(0f, 0.75f, -3.2f);
        camObj.transform.LookAt(new Vector3(0f, 0.7f, 0f));

        camObj.AddComponent<AudioListener>();

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
    }
}
