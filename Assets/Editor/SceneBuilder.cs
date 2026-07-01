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
        AddContactShadow();
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

    private static Material MakeLitColor(string name, Color color)
    {
        var shader = Shader.Find("Standard");
        var mat = new Material(shader) { name = name };
        mat.SetColor("_Color", color);
        mat.SetFloat("_Glossiness", 0.15f);
        mat.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(mat, "Assets/Materials/" + name + ".mat");
        return mat;
    }

    private static void AddGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(2f, 1f, 2f);

        // A lit (not unlit) material so the ground actually receives the key light's shadow -
        // without this the character reads as floating above the floor.
        var mat = MakeLitColor("GroundPastel", new Color(0.95f, 0.90f, 0.85f));
        ground.GetComponent<Renderer>().sharedMaterial = mat;
        ground.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // A soft, procedurally-generated radial-gradient blob under the character's feet. Cheap,
    // reliable way to ground a stylized character even where realtime shadows look too harsh.
    private static void AddContactShadow()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDist = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float alpha = Mathf.SmoothStep(0.55f, 0f, Mathf.Clamp01(dist));
                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        System.IO.File.WriteAllBytes("Assets/Materials/ContactShadow.png", tex.EncodeToPNG());
        AssetDatabase.ImportAsset("Assets/Materials/ContactShadow.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Materials/ContactShadow.png");
        importer.alphaIsTransparency = true;
        importer.textureType = TextureImporterType.Default;
        importer.SaveAndReimport();

        var shadowTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/ContactShadow.png");
        var shader = Shader.Find("Transparent/Diffuse") ?? Shader.Find("Unlit/Transparent");
        var mat = new Material(shader) { name = "ContactShadowMat" };
        mat.mainTexture = shadowTex;
        AssetDatabase.CreateAsset(mat, "Assets/Materials/ContactShadowMat.mat");

        var blob = GameObject.CreatePrimitive(PrimitiveType.Quad);
        blob.name = "ContactShadow";
        Object.DestroyImmediate(blob.GetComponent<Collider>());
        blob.transform.position = new Vector3(0f, 0.01f, 0f);
        blob.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        blob.transform.localScale = new Vector3(0.7f, 0.5f, 1f);
        blob.GetComponent<Renderer>().sharedMaterial = mat;
        blob.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void AddLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);

        // Key light: warm, soft-shadowed, from front-upper-side
        var key = new GameObject("KeyLight").AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.95f, 0.85f);
        key.intensity = 1.15f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.55f;
        key.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

        // Fill light: cool, soft, opposite side, no shadows -- lifts the shadow side gently
        var fill = new GameObject("FillLight").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.78f, 0.86f, 1f);
        fill.intensity = 0.4f;
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(25f, 140f, 0f);

        // Rim light: from behind, gives a subtle edge highlight that separates the character
        // from the background
        var rim = new GameObject("RimLight").AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.color = new Color(1f, 1f, 0.98f);
        rim.intensity = 0.5f;
        rim.shadows = LightShadows.None;
        rim.transform.rotation = Quaternion.Euler(15f, 175f, 0f);
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
