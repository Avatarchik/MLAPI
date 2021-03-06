﻿using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[Serializable]
public class GithubRelease
{
    public string html_url;
    public string tag_name;
    public string name;
    public string body;
    public string published_at;
    public bool prerelease;
    public GithubAsset[] assets;
}

[Serializable]
public class GithubAsset
{
    public string browser_download_url;
    public string name;
}

public class MLAPIEditor : EditorWindow
{
    private GithubRelease[] releases = new GithubRelease[0];
    private bool[] foldoutStatus = new bool[0];
    private string currentVersion
    {
        get
        {
            return EditorPrefs.GetString("MLAPI_version", "None");
        }
        set
        {
            EditorPrefs.SetString("MLAPI_version", value);
        }
    }
    private long lastUpdated
    {
        get
        {
            return Convert.ToInt64(EditorPrefs.GetString("MLAPI_lastUpdated", "0"));
        }
        set
        {
            EditorPrefs.SetString("MLAPI_lastUpdated", Convert.ToString(value));
        }
    }

    [MenuItem("Window/MLAPI")]
    public static void ShowWindow()
    {
        GetWindow<MLAPIEditor>();
    }

    private void OnGUI()
    {
        if (foldoutStatus != null)
        {
            for (int i = 0; i < foldoutStatus.Length; i++)
            {
                if (releases[i] == null)
                    continue;
                foldoutStatus[i] = EditorGUILayout.Foldout(foldoutStatus[i], releases[i].tag_name + " - " + releases[i].name);
                if (foldoutStatus[i])
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Release notes", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(releases[i].body, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.Space();
                    EditorGUILayout.Space();
                    if (releases[i].prerelease)
                    {
                        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                        style.normal.textColor = new Color(1f, 0.5f, 0f);
                        EditorGUILayout.LabelField("Pre-release", style);
                    }
                    else
                    {
                        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                        style.normal.textColor = new Color(0f, 1f, 0f);
                        EditorGUILayout.LabelField("Stable-release", style);
                    }
                    if (currentVersion == releases[i].tag_name)
                    {
                        GUIStyle boldStyle = new GUIStyle(EditorStyles.boldLabel);
                        boldStyle.normal.textColor = new Color(0.3f, 1f, 0.3f);
                        EditorGUILayout.LabelField("Installed", boldStyle);
                    }
                    EditorGUILayout.LabelField("Release date: " + DateTime.Parse(DateTime.Parse(releases[i].published_at).ToString()), EditorStyles.miniBoldLabel);

                    if (currentVersion != releases[i].tag_name && GUILayout.Button("Install"))
                        InstallRelease(i);

                    EditorGUI.indentLevel--;
                }
            }
        }

        GUILayout.BeginArea(new Rect(5, position.height - 40, position.width, 40));

        if (GUILayout.Button("Fetch releases"))
            GetReleases();
        if (GUILayout.Button("Reset defaults"))
        {
            releases = new GithubRelease[0];
            foldoutStatus = new bool[0];
            if (EditorPrefs.HasKey("MLAPI_version")) EditorPrefs.DeleteKey("MLAPI_version");
            if (EditorPrefs.HasKey("MLAPI_lastUpdated")) EditorPrefs.DeleteKey("MLAPI_lastUpdated");
        }

        GUILayout.EndArea();

        string lastUpdatedString = lastUpdated == 0 ? "Never" : new DateTime(lastUpdated).ToShortTimeString();
        EditorGUI.LabelField(new Rect(5, position.height - 60, position.width, 20), "Last checked: " + lastUpdatedString, EditorStyles.centeredGreyMiniLabel);

        if ((releases.Length == 0 && (DateTime.Now - new DateTime(lastUpdated)).TotalSeconds > 600) || (DateTime.Now - new DateTime(lastUpdated)).TotalSeconds > 3600)
            GetReleases();

        Repaint();
    }

    private void InstallRelease(int index)
    {
        for (int i = 0; i < releases[index].assets.Length; i++)
        {
            WWW www = new WWW(releases[index].assets[i].browser_download_url);
            while (!www.isDone && string.IsNullOrEmpty(www.error))
            {
                EditorGUI.ProgressBar(new Rect(5, position.height - 60, position.width, 20), www.progress, "Installing " + i + "/" + releases[index].assets.Length);
            }

            if (!Directory.Exists(Application.dataPath + "/MLAPI/Lib/"))
                Directory.CreateDirectory(Application.dataPath + "/MLAPI/Lib/");

            File.WriteAllBytes(Application.dataPath + "/MLAPI/Lib/" + releases[index].assets[i].name, www.bytes);

            if (releases[index].assets[i].name.EndsWith(".unitypackage"))
                AssetDatabase.ImportPackage(Application.dataPath + "/MLAPI/Lib/" + releases[index].assets[i].name, false);
        }

        currentVersion = releases[index].tag_name;
        AssetDatabase.Refresh();
    }

    private void GetReleases()
    {
        lastUpdated = DateTime.Now.Ticks;

        WWW www = new WWW("https://api.github.com/repos/TwoTenPvP/MLAPI/releases");
        while (!www.isDone && string.IsNullOrEmpty(www.error))
        {
            EditorGUI.ProgressBar(new Rect(5, position.height - 60, position.width, 20), www.progress, "Fetching...");
        }
        string json = www.text;

        //This makes it from a json array to the individual objects in the array. 
        //The JSON serializer cant take arrays. We have to split it up outselves.
        List<string> releasesJson = new List<string>();
        int depth = 0;
        string currentObject = "";
        for (int i = 1; i < json.Length - 1; i++)
        {
            if (json[i] == '[')
                depth++;
            else if (json[i] == ']')
                depth--;
            else if (json[i] == '{')
                depth++;
            else if (json[i] == '}')
                depth--;

            if ((depth == 0 && json[i] != ',') || depth > 0)
                currentObject += json[i];

            if (depth == 0 && json[i] == ',')
            {
                releasesJson.Add(currentObject);
                currentObject = "";
            }
        }

        releases = new GithubRelease[releasesJson.Count];
        foldoutStatus = new bool[releasesJson.Count];

        for (int i = 0; i < releasesJson.Count; i++)
        {
            releases[i] = JsonUtility.FromJson<GithubRelease>(releasesJson[i]);
            if (i == 0)
                foldoutStatus[i] = true;
            else
                foldoutStatus[i] = false;
        }
    }
}
