﻿using UnityEngine;
using System.Collections;
using Microsoft.Win32;

public class EnvPaths : MonoBehaviour {

	public static string DefaultMapPath;
	public static string DefaultGamedataPath;

	static RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");


	const string InstalationPath = "InstalationPath";
	const string InstalationGamedata = "gamedata/";
	const string MapsPath = "MapsPath";

	public static string GetInstalationPath(){
		return PlayerPrefs.GetString (InstalationPath, EnvPaths.DefaultGamedataPath);
	}

	public static void SetInstalationPath(string value){
		value = value.Replace("\\", "/");
		if(value[value.Length - 1].ToString() != "/") value += "/";
		if(value[0].ToString() == "/") value = value.Remove(0,1);

		PlayerPrefs.SetString (InstalationPath, value);
	}

	public static string GetGamedataPath(){
		return GetInstalationPath() + InstalationGamedata;
	}

	public static void SetMapsPath(string value){
		value = value.Replace("\\", "/");
		if(value[value.Length - 1].ToString() != "/") value += "/";
		if(value[0].ToString() == "/") value = value.Remove(0,1);

		PlayerPrefs.SetString (MapsPath, value);
	}

	public static string GetMapsPath(){
		return PlayerPrefs.GetString(MapsPath, EnvPaths.DefaultMapPath);
	}


	#region Auto Generate
	public static void GenerateDefaultPaths(){
		GenerateMapPath ();
		GenerateGamedataPath ();
	}

	public static void GenerateMapPath(){
		DefaultMapPath = System.Environment.GetFolderPath (System.Environment.SpecialFolder.MyDocuments).Replace ("\\", "/") + "/My Games/Gas Powered Games/Supreme Commander Forged Alliance/Maps/";
		if (!System.IO.Directory.Exists (DefaultMapPath)) {
			Debug.LogWarning ("Default map directory not exist: " + DefaultMapPath);
			DefaultMapPath = "maps/";
		}
	}

	public static void GenerateGamedataPath(){
		DefaultGamedataPath = FindByDisplayName(regKey, "Supreme Commander: Forged Alliance").Replace("\\", "/");
		if (!string.IsNullOrEmpty (DefaultGamedataPath)) {
			if (!DefaultGamedataPath.EndsWith ("/"))
				DefaultGamedataPath += "/";

			if (!System.IO.Directory.Exists (DefaultGamedataPath)) {
				Debug.LogWarning ("Instalation directory not exist: " + DefaultGamedataPath);
				DefaultGamedataPath = "";
			}
		}

		if (string.IsNullOrEmpty (DefaultGamedataPath))
			DefaultGamedataPath = "gamedata/";
	}


	private static string FindByDisplayName(RegistryKey parentKey, string name)
	{
		string[] nameList = parentKey.GetSubKeyNames();
		for (int i = 0; i < nameList.Length; i++)
		{
			RegistryKey regKey =  parentKey.OpenSubKey(nameList[i]);
			try
			{
				if (regKey.GetValue("DisplayName").ToString() == name)
				{
					return regKey.GetValue("InstallLocation").ToString();
				}
			}
			catch { }
		}
		return "";
	}
	#endregion
}