﻿using UnityEngine;
using UnityEditor;
using System.IO;
using System;

public class LyumaShader2dScript : ScriptableObject {
    //[MenuItem ("Tools/Shader2d")]
    //MenuItem("GameObject/Create Mesh")
    [MenuItem ("CONTEXT/Material/Make 2d (LyumaShader2d)")]
    static void Shader2dMaterial (MenuCommand command)
    {
        Material m = command.context as Material;
        Shader newShader = Shader2d(m.shader);
        if (newShader != null) {
            m.shader = newShader;
        }
    }

    [MenuItem ("CONTEXT/Shader/Generate 2d shader (LyumaShader2d)")]
    static void Shader2dShader (MenuCommand command)
    {
        Shader s = command.context as Shader;
        Shader newS = Shader2d (s);
        EditorGUIUtility.PingObject (newS);
    }
    static Shader Shader2d (Shader s) {
        string shaderName = s.name;
        string path = AssetDatabase.GetAssetPath (s);
        Debug.Log ("Starting to work on shader " + shaderName);
        Debug.Log ("Original path: " + path);
        if (path.StartsWith("Resources/unity_builtin_extra", StringComparison.CurrentCulture) && "Standard".Equals(s.name)) {
            string [] tmpassets = AssetDatabase.FindAssets ("StandardSimple");
            foreach (string guid in tmpassets) {
                path = AssetDatabase.GUIDToAssetPath (guid);
                if (path.IndexOf(".shader", StringComparison.CurrentCulture) != -1) {
                    break;
                }
            }
        }
        string [] shaderData = File.ReadAllLines (path);
        int state = 0;
        int comment = 0;
        int braceLevel = 0;
        int lineNum = -1;
        int endPropertiesLineNum = -1;
        int endPropertiesSkip = -1;
        bool foundCgInclude = false;
        bool foundNoCgInclude = false;
        int cgIncludeLineNum = -1;
        int cgIncludeSkip = -1;
        int editShaderNameLineNum = -1;
        int editShaderNameSkip = -1;
        foreach (string xline in shaderData) {
            string line = xline;
            if (line.IndexOf ("Shader2d Generated", StringComparison.CurrentCulture) != -1) {
                EditorUtility.DisplayDialog ("Shader2d", "Detected an existing Shader2d comment: " + shaderName + " is already 2d!", "OK", "");
                endPropertiesLineNum = -1;
                state = 5;
                return null;
            }
            lineNum++;
            int lineSkip = 0;
            while (true) {
                Debug.Log ("Looking for comment " + lineNum);
                int commentIdx;
                if (comment == 1) {
                    commentIdx = line.IndexOf ("*/", lineSkip, StringComparison.CurrentCulture);
                    if (commentIdx != -1) {
                        lineSkip = commentIdx + 2;
                        comment = 0;
                    } else {
                        line = "";
                        break;
                    }
                }
                commentIdx = line.IndexOf ("//", lineSkip, StringComparison.CurrentCulture);
                if (commentIdx != -1) {
                    line = line.Substring (0, commentIdx);
                    break;
                }
                commentIdx = line.IndexOf ("/*", lineSkip, StringComparison.CurrentCulture);
                if (commentIdx != -1) {
                    int endCommentIdx = line.IndexOf ("*/", lineSkip, StringComparison.CurrentCulture);
                    if (endCommentIdx != -1) {
                        line = line.Substring (0, commentIdx) + new String (' ', (endCommentIdx + 2 - commentIdx)) + line.Substring (endCommentIdx + 2);
                        lineSkip = endCommentIdx + 2;
                    } else {
                        line = line.Substring (0, commentIdx);
                        comment = 1;
                        break;
                    }
                } else {
                    break;
                }
            }
            bool fallThrough = true;
            while (fallThrough) {
                Debug.Log ("Looking for state " + state + " on line " + lineNum);
                fallThrough = false;
                switch (state) {
                case 0: {
                        int shaderOff = line.IndexOf ("Shader", lineSkip, StringComparison.CurrentCulture);
                        if (shaderOff != -1) {
                            int firstQuote = line.IndexOf ('\"', shaderOff);
                            int secondQuote = line.IndexOf ('\"', firstQuote + 1);
                            if (firstQuote != -1 && secondQuote != -1) {
                                editShaderNameLineNum = lineNum;
                                editShaderNameSkip = secondQuote;
                                state = 1;
                            }
                        }
                    }
                    break;
                case 1: {
                        // Find beginning of Properties block
                        int shaderOff = line.IndexOf ("Properties", lineSkip, StringComparison.CurrentCulture);
                        if (shaderOff != -1) {
                            state = 2;
                            line = line.Substring (shaderOff);
                            lineSkip = shaderOff;
                            fallThrough = true;
                        }
                    }
                    break;
                case 2: {
                        // Find end of Properties block
                        while (lineSkip < line.Length) {
                            Debug.Log ("Looking for braces state " + state + " on line " + lineNum + "/" + lineSkip + " {}" + braceLevel);
                            int openBrace = line.IndexOf ("{", lineSkip, StringComparison.CurrentCulture);
                            int closeBrace = line.IndexOf ("}", lineSkip, StringComparison.CurrentCulture);
                            if (closeBrace != -1 && (openBrace > closeBrace || openBrace == -1)) {
                                braceLevel--;
                                if (braceLevel == 0) {
                                    endPropertiesLineNum = lineNum;
                                    endPropertiesSkip = lineSkip;
                                    state = 3;
                                    fallThrough = true;
                                }
                                lineSkip = closeBrace + 1;
                            } else if (openBrace != -1 && (openBrace < closeBrace || closeBrace == -1)) {
                                braceLevel++;
                                lineSkip = openBrace + 1;
                            } else {
                                break;
                            }
                        }
                        /*
                            string[] quotes = line.Substring (shaderOff).split ("{");
                            string shader[1]
                        }*/
                    }
                    break;
                case 3: {
                        // Find beginning of CGINCLUDE block, or beginning of a Pass or CGPROGRAM
                        int cgInclude = line.IndexOf ("CGINCLUDE", lineSkip, StringComparison.CurrentCulture);
                        int cgProgram = line.IndexOf ("CGPROGRAM", lineSkip, StringComparison.CurrentCulture);
                        int passBlock = line.IndexOf ("GrabPass", lineSkip, StringComparison.CurrentCulture);
                        int grabPassBlock = line.IndexOf ("Pass", lineSkip, StringComparison.CurrentCulture);
                        if (cgInclude != -1) {
                            foundCgInclude = true;
                        } else if (cgProgram != -1) {
                            foundNoCgInclude = true;
                        } else if (grabPassBlock != -1) {
                            foundNoCgInclude = true;
                        } else if (passBlock != -1) {
                            if (passBlock == lineSkip || char.IsWhiteSpace (line [passBlock - 1])) {
                                if (passBlock + 4 == line.Length || char.IsWhiteSpace (line [passBlock + 4])) {
                                    foundNoCgInclude = true;
                                }
                            }
                        }
                        if (foundCgInclude) {
                            state = 4;
                            cgIncludeLineNum = lineNum + 1;
                            cgIncludeSkip = 0;
                        } else if (foundNoCgInclude) {
                            state = 4;
                            cgIncludeLineNum = lineNum;
                            cgIncludeSkip = lineSkip;
                        }
                    }
                    break;
                case 4:
                    // Look for modified tag, or end of shader, or custom editor.
                    break;
                }
            }
            if (state == 5) {
                break;
            }
        }
        Debug.Log ("Done with hard work");
        if (editShaderNameLineNum == -1) {
            EditorUtility.DisplayDialog ("Shader2d", "In " + shaderName + ": failed to find Shader \"...\" block.", "OK", "");
            // Failed to parse shader;
            return null;
        }
        if (endPropertiesLineNum == -1) {
            EditorUtility.DisplayDialog ("Shader2d", "In " + shaderName + ": failed to find end of Properties block.", "OK", "");
            // Failed to parse shader;
            return null;
        }
        if (cgIncludeLineNum == -1) {
            EditorUtility.DisplayDialog ("Shader2d", "In " + shaderName + ": failed to find CGINCLUDE or appropriate insertion point.", "OK", "");
            // Failed to parse shader;
            return null;
        }
        string shaderLine = shaderData [editShaderNameLineNum];
        shaderLine = shaderLine.Substring (0, editShaderNameSkip) + "_2d" + shaderLine.Substring (editShaderNameSkip);
        shaderData [editShaderNameLineNum] = shaderLine;

        string epLine = shaderData [endPropertiesLineNum];
        string propertiesAdd = "// Shader2d Properties:\n" +
            "        _2d_coef (\"Twodimensionalness\", Range(0, 1)) = 1.0\n" +
            "        _facing_coef (\"Facing Lock\", Range (0, 1)) = 0.0\n" +
            "        _lock2daxis_coef (\"Lock 2d Axis\", Range (0, 1)) = 0.0\n" +
            "        _ztweak_coef (\"Tweak z axis\", Range (-1, 1)) = 0.0\n";
        epLine = epLine.Substring (0, endPropertiesSkip) + propertiesAdd + epLine.Substring (endPropertiesSkip);
        shaderData [endPropertiesLineNum] = epLine;

        string [] shader2dassets = AssetDatabase.FindAssets ("Shader2d");
        string includePath = "LyumaShader/Shader2d/Shader2d.cginc";
        foreach (string guid in shader2dassets) {
            Debug.Log ("testI: " + AssetDatabase.GUIDToAssetPath (guid));
            includePath = AssetDatabase.GUIDToAssetPath (guid);
            if (!includePath.Contains("Shader2d.cginc")) {
                includePath += "/Shader2d.cginc";
            }
            if (!includePath.StartsWith ("Assets/", StringComparison.CurrentCulture)) {
                EditorUtility.DisplayDialog ("Shader2d", "This script at path " + includePath + " must be in Assets!", "OK", "");
                return null;
            }
            includePath = includePath.Substring (7);
        }
        int numSlashes = 0;
        if (!path.StartsWith("Assets/", StringComparison.CurrentCulture)) {
            EditorUtility.DisplayDialog ("Shader2d", "Shader " + shaderName + " at path " + path + " must be in Assets!", "OK", "");
            return null;
        }
        string includePrefix = "";
        foreach (char c in path.Substring(7)) {
            if (c == '/') {
                numSlashes++;
                includePrefix += "../";
            }
        }
        includePath = includePrefix + includePath;
        if (foundCgInclude) {
            string cgIncludeLine = shaderData [cgIncludeLineNum];
            string cgIncludeAdd = "//Shader2d Generated\n#define LYUMA2D_HOTPATCH\n#include \"" + includePath + "\"\n";
            shaderData [cgIncludeLineNum] = cgIncludeAdd + cgIncludeLine;
        } else {
            string cgIncludeLine = shaderData [cgIncludeLineNum];
            string cgIncludeAdd = "\nCGINCLUDE\n//Shader2d Generated Block\n#define LYUMA2D_HOTPATCH\n#include \"" + includePath + "\"\nENDCG\n";
            shaderData [cgIncludeLineNum] = cgIncludeLine.Substring (0, cgIncludeSkip) + cgIncludeAdd + cgIncludeLine.Substring (cgIncludeSkip);
        }
        String dest = path.Replace (".shader", "_2d.txt");
        String finalDest = path.Replace (".shader", "_2d.shader");
        if (dest.Equals(path)) {
            EditorUtility.DisplayDialog ("Shader2d", "Shader " + shaderName + " at path " + path + " does not have .shader!", "OK", "");
            return null;
        }
        Debug.Log ("Writing shader " + dest);
        Debug.Log ("Shader name" + shaderName + "_2d");
        Debug.Log ("Original path " + path + " name " + shaderName);
        StreamWriter writer = new StreamWriter (dest, false);
        for (int i = 0; i < shaderData.Length; i++) {
            if (shaderData [i].IndexOf ("CustomEditor", StringComparison.CurrentCulture) != -1) {
                writer.WriteLine ("//" + shaderData [i]);
            } else {
                writer.WriteLine (shaderData [i]);
            }
        }
        writer.Close ();
        FileUtil.ReplaceFile (dest, finalDest);
        try {
            FileUtil.DeleteFileOrDirectory (dest);
        } catch (Exception e) {
        }
        //FileUtil.MoveFileOrDirectory (dest, finalDest);
        AssetDatabase.ImportAsset (finalDest);
        return (Shader)AssetDatabase.LoadAssetAtPath (finalDest, typeof(Shader));
    }
}
