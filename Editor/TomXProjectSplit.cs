using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// One-time, explicit Bridge migration. Never runs automatically on import.
public static class TomXProjectSplit
{
    const string Old = "Assets/Mods/KKShadersPlus-release1.7.1";
    const string Root = "Assets/Mods/TomShadersX";
    const string Bundle = "chara/tom/shaders/tomx.unity3d";
    const string Backup = "CodexBridge/Backups/XSplit-20261002";
    static readonly string[] Names = { "MainOpaqueX", "MainAlphaX", "MainAlphaX2Pass" };
    [Serializable] class Entry { public string source, destination, guid; }
    [Serializable] class Journal { public List<Entry> entries = new List<Entry>(); }

    public static string Migrate()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play Mode first.");
        var moves = new List<Entry>();
        Action<string,string> add = (src,dst) => moves.Add(new Entry { source=src, destination=dst, guid=AssetDatabase.AssetPathToGUID(src) });
        add(Old+"/Shaders/Tom",Root+"/Shaders/Tom");
        foreach(string n in Names) {
            add(Old+"/Material/m_Tom"+n+".mat",Root+"/Material/m_Tom"+n+".mat");
            add(Old+"/Prefab/a_Tom"+n+".prefab",Root+"/Prefab/a_Tom"+n+".prefab");
        }
        foreach(string path in Directory.GetFiles(Old+"/Editor","TomX*.cs"))
            add(path.Replace('\\','/'),Root+"/Editor/"+Path.GetFileName(path));
        foreach(string path in Directory.GetFiles(Old+"/Tests").Where(p=>!p.EndsWith(".meta")))
            add(path.Replace('\\','/'),Root+"/Tests/"+Path.GetFileName(path));
        foreach(string path in Directory.GetFiles(Old+"/Documents","XSeries*.md"))
            add(path.Replace('\\','/'),Root+"/Documents/"+Path.GetFileName(path));
        foreach(string path in Directory.GetDirectories(Old+"/Documents/Evidence"))
            add(path.Replace('\\','/'),Root+"/Documents/Evidence/"+Path.GetFileName(path));
        add(Old+"/Tooltips/tom_x_tooltips.xml",Root+"/Tooltips/tom_x_tooltips.xml");
        add("Assets/TomXStageTwoAcceptance",Root+"/Tests/ReferenceAssets");
        foreach(Entry e in moves) {
            if(string.IsNullOrEmpty(e.guid)) throw new Exception("Missing source: "+e.source);
            if(File.Exists(e.destination) || Directory.Exists(e.destination))
                throw new Exception("Destination already exists: "+e.destination);
        }
        Directory.CreateDirectory(Backup);
        string archive=Backup+"/before-split.unitypackage";
        if(!File.Exists(archive))
            AssetDatabase.ExportPackage(moves.Select(e=>e.source).Concat(new[] {Old+"/manifest.xml",Old+"/Shaders/KKPDeclarations.cginc",Old+"/LICENSE"}).ToArray(),
                archive,ExportPackageOptions.Recurse);
        var journal=new Journal();
        File.WriteAllText(Backup+"/move-plan.json",JsonUtility.ToJson(new Journal { entries=moves },true));
        string emptyDuplicate=Root+"/Shaders 1";
        if(Directory.Exists(emptyDuplicate) && Directory.GetFileSystemEntries(emptyDuplicate).Length==0)
            if(!AssetDatabase.DeleteAsset(emptyDuplicate)) throw new Exception("Cannot remove empty failed-migration folder.");
        foreach(Entry e in moves) Folder(Path.GetDirectoryName(e.destination).Replace('\\','/'));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(Entry e in moves)
            if(!AssetDatabase.IsValidFolder(Path.GetDirectoryName(e.destination).Replace('\\','/')))
                throw new Exception("Unregistered parent: "+e.destination);
        CopyOnce(Old+"/Shaders/KKPDeclarations.cginc",Root+"/Shaders/KKPDeclarations.cginc");
        CopyOnce(Old+"/LICENSE",Root+"/LICENSE");
        AssetDatabase.StartAssetEditing();
        try {
            foreach(Entry e in moves) {
                string error=AssetDatabase.MoveAsset(e.source,e.destination);
                if(!string.IsNullOrEmpty(error)) throw new Exception(error);
                journal.entries.Add(e);
                File.WriteAllText(Backup+"/move-journal.json",JsonUtility.ToJson(journal,true));
            }
        }
        catch(Exception error) {
            var failures=new List<string>();
            for(int i=journal.entries.Count-1;i>=0;i--) {
                Entry e=journal.entries[i];
                string rollback=AssetDatabase.MoveAsset(e.destination,e.source);
                if(!string.IsNullOrEmpty(rollback)) failures.Add(rollback);
            }
            throw new Exception("Split failed: "+error.Message+"; rollback issues: "+string.Join("; ",failures));
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        foreach(string n in Names) {
            AssetImporter importer=AssetImporter.GetAtPath(Root+"/Prefab/a_Tom"+n+".prefab");
            importer.assetBundleName=Bundle; importer.assetBundleVariant=""; importer.SaveAndReimport();
        }
        AssetImporter catalog=AssetImporter.GetAtPath(Root+"/Tooltips/tom_x_tooltips.xml");
        catalog.assetBundleName=Bundle; catalog.assetBundleVariant=""; catalog.SaveAndReimport();
        AssetDatabase.SaveAssets();
        return "Moved "+journal.entries.Count+" asset roots preserving GUIDs; common include copied; new bundle "+Bundle+". "+Validate();
    }

    public static string Validate()
    {
        var report=new StringBuilder();
        Journal journal=JsonUtility.FromJson<Journal>(File.ReadAllText(Backup+"/move-journal.json"));
        foreach(Entry e in journal.entries) {
            if(AssetDatabase.AssetPathToGUID(e.destination)!=e.guid)
                throw new Exception("GUID changed: "+e.destination);
            if(File.Exists(e.source)||Directory.Exists(e.source))
                throw new Exception("Old source remains: "+e.source);
        }
        string a=File.ReadAllText(Old+"/Shaders/KKPDeclarations.cginc");
        string b=File.ReadAllText(Root+"/Shaders/KKPDeclarations.cginc");
        if(a!=b) throw new Exception("Shared code differs at split.");
        if(AssetDatabase.AssetPathToGUID(Old+"/Shaders/KKPDeclarations.cginc")==AssetDatabase.AssetPathToGUID(Root+"/Shaders/KKPDeclarations.cginc"))
            throw new Exception("Copied asset must have a new GUID.");
        foreach(string n in Names) {
            string matPath=Root+"/Material/m_Tom"+n+".mat";
            string prefabPath=Root+"/Prefab/a_Tom"+n+".prefab";
            Material m=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            GameObject p=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if(m==null || m.shader==null || m.shader.name!="tom/"+n || !m.shader.isSupported)
                throw new Exception("Bad material: "+matPath);
            if(p==null || p.GetComponent<Renderer>().sharedMaterial!=m)
                throw new Exception("Bad prefab: "+prefabPath);
            if(AssetDatabase.GetAssetPath(m.shader)!=Root+"/Shaders/Tom/"+n+".shader")
                throw new Exception("Shader not resolved inside standalone project.");
            if(AssetImporter.GetAtPath(prefabPath).assetBundleName!=Bundle)
                throw new Exception("Wrong bundle: "+prefabPath);
            foreach(string d in AssetDatabase.GetDependencies(prefabPath,true))
                if(d.StartsWith(Old+"/",StringComparison.Ordinal)) throw new Exception("Cross-project dependency: "+d);
            report.Append(n).Append(": binding and dependency closure OK; ");
        }
        string[] bundleNames=AssetDatabase.GetAllAssetBundleNames();
        foreach(string name in bundleNames)
            if(AssetDatabase.GetAssetPathsFromAssetBundle(name).Any(p=>p.StartsWith(Root+"/",StringComparison.Ordinal)) && name!=Bundle)
                throw new Exception("New assets still assigned to legacy bundle: "+name);
        return report+"GUID preservation and independent shared include verified.";
    }
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path)) return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');
        Folder(parent);
        if(string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent,Path.GetFileName(path))))
            throw new Exception("Cannot create "+path);
    }
    static void CopyOnce(string source,string destination)
    {
        if(File.Exists(destination)) {
            if(!File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(destination)))
                throw new Exception("Refusing to replace differing copy: "+destination);
            return;
        }
        if(!AssetDatabase.CopyAsset(source,destination)) throw new Exception("Copy failed: "+source);
    }
}
