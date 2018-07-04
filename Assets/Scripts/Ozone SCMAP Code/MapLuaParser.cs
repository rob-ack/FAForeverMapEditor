﻿// ***************************************************************************************
// * Simple SupCom map LUA parser
// * TODO : should read all values. Right now it only search for known, hardcoded values
// ***************************************************************************************

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EditMap;
using NLua;
using MapLua;
using FAF.MapEditor;

public partial class MapLuaParser : MonoBehaviour
{

	public static MapLuaParser Current;

	[Header("LUA")]
	public string LoadedMapFolder;
	public ScenarioLua ScenarioLuaFile;
	public SaveLua SaveLuaFile;
	public TablesLua TablesLuaFile;
	public TextAsset SaveLuaHeader;
	public TextAsset SaveLuaFooter;
	public TextAsset DefaultScript;
	public TextAsset AdaptiveScript;
	public TextAsset AdaptiveOptions;

	#region Variables
	[Header("Core")]
	public ScmapEditor HeightmapControler;
	public Editing EditMenu;
	public Undo History;

	[Header("Map")]
	public string FolderParentPath;
	public string FolderName;
	public string ScenarioFileName;

	public static string LoadedMapFolderPath
	{
		get
		{
			return Current.FolderParentPath + Current.FolderName + "/";
		}
	}

	public static string RelativeLoadedMapFolderPath
	{
		get
		{
			return "maps/" + Current.FolderName + "/";
		}
	}

	//public		CameraControler	CamControll;
	[Header("UI")]
	public GameObject Background;
	public GenericInfoPopup InfoPopup;
	public GenericInfoPopup ErrorPopup;
	public PropsInfo PropsMenu;
	public DecalsInfo DecalsMenu;
	public UnitsInfo UnitsMenu;

	[Header("Local Data")]
	public Vector3 MapCenterPoint;

	public int ScriptId = 0;

	public static string BackupPath;
	public static string StructurePath;


	#endregion


	void Awake()
	{
		Current = this;
		LoadStructurePaths();

		DecalsInfo.Current = DecalsMenu;
		PropsInfo.Current = PropsMenu;
		UnitsInfo.Current = UnitsMenu;
	}

	public static void LoadStructurePaths()
	{
		ICSharpCode.SharpZipLib.Zip.ZipConstants.DefaultCodePage = 0;

		EnvPaths.GenerateDefaultPaths();

		if (string.IsNullOrEmpty(EnvPaths.GetInstalationPath()))
		{
			EnvPaths.GenerateGamedataPath();
			EnvPaths.SetInstalationPath(EnvPaths.DefaultGamedataPath);
		}

		StructurePath = Application.dataPath + "/Structure/"; ;
#if UNITY_EDITOR
		StructurePath = StructurePath.Replace("Assets", "");
#endif
	}

	public static bool IsMapLoaded
	{
		get
		{
			return !string.IsNullOrEmpty(Current.FolderName) && !string.IsNullOrEmpty(Current.ScenarioFileName) && !string.IsNullOrEmpty(Current.FolderParentPath);
		}
	}

	public void ResetUI()
	{
		WindowStateSever.WindowStateSaver.ChangeWindowName("");

		EditMenu.ButtonFunction("Map");
		EditMenu.gameObject.SetActive(false);
		Background.SetActive(true);

		FolderParentPath = "";
		FolderName = "";
		ScenarioFileName = "";

		EditMenu.TexturesMenu.ResetVisibility();

		ResourceBrowser.Current.gameObject.SetActive(false);
	}

	#region Loading

	public IEnumerator ForceLoadMapAtPath(string path, bool Props, bool Decals)
	{
		path = path.Replace("\\", "/");
		Debug.Log("Load from: " + path);

		char[] NameSeparator = ("/").ToCharArray();
		string[] Names = path.Split(NameSeparator);

		FolderParentPath = "";

		for (int i = 0; i < Names.Length - 2; i++)
			FolderParentPath += Names[i] + "/";

		FolderName = Names[Names.Length - 2];
		ScenarioFileName = Names[Names.Length - 1].Replace(".lua", "");
		//string NewMapPath = path.Replace(FolderName + "/" + ScenarioFileName + ".lua", "");

		Debug.Log("Parsed args: \n"
			+ FolderName + "\n"
			+ ScenarioFileName + "\n"
			+ FolderName);


		//PlayerPrefs.SetString("MapsPath", NewMapPath);
		loadSave = false;
		LoadProps = Props;
		LoadDecals = Decals;

		var LoadFile = StartCoroutine("LoadingFile");
		yield return LoadFile;

		InfoPopup.Show(false);
		//PlayerPrefs.SetString("MapsPath", LastMapPatch);
	}

	public void LoadFile()
	{
		if (LoadingMapProcess)
			return;
		LoadingMapProcess = true;
		ScmapEditor.Current.UnloadMap();
		LoadingMapProcess = false;

		StartCoroutine("LoadingFile");
	}

	bool loadSave = true;
	bool LoadProps = true;
	bool LoadDecals = true;

	public static bool LoadingMapProcess = false;
	public static Coroutine LoadScmapFile;
	IEnumerator LoadingFile()
	{

		while (SavingMapProcess)
			yield return null;

		Undo.Current.Clear();

		bool AllFilesExists = true;
		string Error = "";
		if (!System.IO.Directory.Exists(FolderParentPath))
		{
			Error = "Map folder does not exist:\n" + FolderParentPath;
			Debug.LogWarning(Error);
			AllFilesExists = false;
		}

		if (AllFilesExists && !System.IO.File.Exists(LoadedMapFolderPath + ScenarioFileName + ".lua"))
		{
			AllFilesExists = SearchForScenario();

			if (!AllFilesExists)
			{
				Error = "Scenario.lua does not exist:\n" + LoadedMapFolderPath + ScenarioFileName + ".lua";
				Debug.LogWarning(Error);
			}
		}

		if (AllFilesExists)
		{
			string ScenarioText = System.IO.File.ReadAllText(LoadedMapFolderPath + ScenarioFileName + ".lua");

			if (!ScenarioText.StartsWith("version = 3") && ScenarioText.StartsWith("version ="))
			{
				AllFilesExists = SearchForScenario();

				if (!AllFilesExists)
				{
					Error = "Wrong scenario file version. Should be 3, is " + ScenarioText.Remove(11).Replace("version =", "");
					Debug.LogWarning(Error);
				}
			}


			if (AllFilesExists && !ScenarioText.StartsWith("version = 3"))
			{
				AllFilesExists = SearchForScenario();

				if (!AllFilesExists)
				{
					Error = "Selected file is not a proper scenario.lua file. ";
					Debug.LogWarning(Error);
				}
			}
		}

		if (AllFilesExists && !System.IO.File.Exists(EnvPaths.GamedataPath + "/env.scd"))
		{
			Error = "No source files in gamedata folder: " + EnvPaths.GamedataPath;
			Debug.LogWarning(Error);
			AllFilesExists = false;
		}

		if (AllFilesExists)
		{
			// Begin load
			LoadRecentMaps.MoveLastMaps(ScenarioFileName, FolderName, FolderParentPath);
			LoadingMapProcess = true;
			InfoPopup.Show(true, "Loading map...\n( " + ScenarioFileName + ".lua" + " )");
			EditMenu.gameObject.SetActive(true);
			Background.SetActive(false);
			yield return null;

			ScenarioLuaFile = new ScenarioLua();
			SaveLuaFile = new SaveLua();
			TablesLuaFile = new TablesLua();
			AsyncOperation ResUn = Resources.UnloadUnusedAssets();
			while (!ResUn.isDone)
			{
				yield return null;
			}

			// Scenario LUA
			if (ScenarioLuaFile.Load(FolderName, ScenarioFileName, FolderParentPath))
			{
				//Map Loaded
			}


			CameraControler.Current.MapSize = Mathf.Max(ScenarioLuaFile.Data.Size[0], ScenarioLuaFile.Data.Size[1]);
			CameraControler.Current.RestartCam();


			InfoPopup.Show(true, "Loading map...\n(" + ScenarioLuaFile.Data.map + ")");
			yield return null;

			// SCMAP
			LoadScmapFile = HeightmapControler.StartCoroutine(ScmapEditor.Current.LoadScmapFile());
			yield return LoadScmapFile;
			CameraControler.Current.RestartCam();

			EditMenu.MapInfoMenu.SaveAsFa.isOn = HeightmapControler.map.VersionMinor >= 60;
			EditMenu.MapInfoMenu.SaveAsSc.isOn = !EditMenu.MapInfoMenu.SaveAsFa.isOn;

			InfoPopup.Show(true, "Loading map...\n(" + ScenarioLuaFile.Data.save + ")");
			yield return null;

			if (loadSave)
			{
				// Save LUA
				SaveLuaFile.Load();
				SetSaveLua();
				//LoadSaveLua();
				yield return null;

				Coroutine UnitsLoader = StartCoroutine(SaveLuaFile.LoadUnits());
				yield return UnitsLoader;
			}


			// Load Props
			if (LoadProps)
			{
				PropsMenu.gameObject.SetActive(true);

				PropsMenu.AllowBrushUpdate = false;
				PropsMenu.StartCoroutine(PropsMenu.LoadProps());
				while (PropsMenu.LoadingProps)
				{
					InfoPopup.Show(true, "Loading map...\n( Loading props " + PropsMenu.LoadedCount + "/" + ScmapEditor.Current.map.Props.Count + ")");
					yield return null;
				}

				PropsMenu.gameObject.SetActive(false);
			}

			if (LoadDecals)
			{
				DecalsMenu.gameObject.SetActive(true);

				//DecalsMenu.AllowBrushUpdate = false;
				DecalsMenu.StartCoroutine(DecalsMenu.LoadDecals());
				while (DecalsInfo.LoadingDecals)
				{
					InfoPopup.Show(true, "Loading map...\n( Loading decals " + DecalsMenu.LoadedCount + "/" + ScmapEditor.Current.map.Decals.Count + ")");
					yield return null;
				}

				DecalsMenu.gameObject.SetActive(false);
			}

			if (TablesLuaFile.Load(FolderName, ScenarioFileName, FolderParentPath))
			{
				//Map Loaded
			}

			// Fix map version
			if (FolderName.Contains(".v"))
			{
				int ParsedValue = (int)ScenarioLuaFile.Data.map_version;

				if (int.TryParse(FolderName.Remove(0, FolderName.Length - 4), out ParsedValue))
				{
					ScenarioLuaFile.Data.map_version = ParsedValue;
				}
			}


			InfoPopup.Show(false);
			WindowStateSever.WindowStateSaver.ChangeWindowName(FolderName);
			LoadingMapProcess = false;


			RenderMarkersConnections.Current.UpdateConnections();

			EditMenu.Categorys[0].GetComponent<MapInfo>().UpdateFields();

			MapLuaParser.Current.UpdateArea();

			GenericInfoPopup.ShowInfo("Map successfully loaded!\n" + FolderName + "/" + ScenarioFileName + ".lua");
		}
		else
		{
			ResetUI();
			ReturnLoadingWithError(Error);

		}

	}

	public void ReturnLoadingWithError(string Error)
	{
		//Map = false;
		//OpenComposition(0);
		ErrorPopup.Show(true, Error);
		ErrorPopup.InvokeHide();
	}

	bool SearchForScenario()
	{
		if (!System.IO.Directory.Exists(LoadedMapFolderPath))
			return false;

		string[] AllFiles = System.IO.Directory.GetFiles(LoadedMapFolderPath);
		for (int i = 0; i < AllFiles.Length; i++)
		{
			if (AllFiles[i].ToLower().EndsWith(".lua"))
			{
				if (System.IO.File.ReadAllText(AllFiles[i]).StartsWith("version = 3"))
				{
					//Found Other Proper File
					Debug.Log(AllFiles[i]);

					string[] Names = AllFiles[i].Replace("\\", "/").Split("/".ToCharArray());
					ScenarioFileName = Names[Names.Length - 1].Replace(".lua", "");
					Debug.Log(ScenarioFileName);

					return true;
				}
			}
		}
		return false;
	}

	#endregion

	#region Load Save Lua
	void SetSaveLua()
	{
		UpdateArea();

		//MapElements.SetActive(false);

		MapCenterPoint = Vector3.zero;
		MapCenterPoint.x = (GetMapSizeX() / 20f);
		MapCenterPoint.z = -1 * (GetMapSizeY() / 20f);

		//SortArmys();
	}
	#endregion

	#region SaveMap

	public bool BackupFiles = true;
	public void SaveMap(bool Backup = true)
	{
		if (!IsMapLoaded)
			return;



		BackupFiles = Backup;

		Debug.Log("Save map");

		SavingMapProcess = true;
		InfoPopup.Show(true, "Saving map...");

		StartCoroutine(SaveMapProcess());
	}

	public static bool SavingMapProcess = false;
	public IEnumerator SaveMapProcess()
	{

		yield return null;


		// Wait for all process to finish
		while (Markers.MarkersControler.IsUpdating)
			yield return null;
		while (PropsRenderer.IsUpdating)
			yield return null;
		while (UnitsControler.IsUpdating)
			yield return null;
		while (DecalsControler.IsUpdating)
			yield return null;


		GenerateBackupPath();
		PreviewTex.ForcePreviewMode(true);
		yield return null;

		// Scenario.lua
		string ScenarioFilePath = LoadedMapFolderPath + ScenarioFileName + ".lua";
		if (BackupFiles && System.IO.File.Exists(ScenarioFilePath))
			System.IO.File.Move(ScenarioFilePath, BackupPath + "/" + ScenarioFileName + ".lua");
		ScenarioLuaFile.Save(ScenarioFilePath);
		yield return null;

	

		//Save.lua
		string SaveFilePath = MapRelativePath(ScenarioLuaFile.Data.save);
		string FileName = ScenarioLuaFile.Data.save;
		string[] Names = FileName.Split(("/").ToCharArray());
		if (BackupFiles && System.IO.File.Exists(SaveFilePath))
			System.IO.File.Move(SaveFilePath, BackupPath + "/" + Names[Names.Length - 1]);

		SaveLuaFile.Save(SaveFilePath);
		yield return null;

		//SaveScenarioLua();
		//SaveSaveLua();
		//SaveScriptLua(ScriptId);

		SaveScmap();
		yield return null;

		SaveTablesLua();
		yield return null;

		InfoPopup.Show(false);
		SavingMapProcess = false;
		GenericInfoPopup.ShowInfo("Map saved!\n" + FolderName + "/" + ScenarioFileName + ".lua" );
	}

	void GenerateBackupPath()
	{
		string BackupId = System.DateTime.Now.Month.ToString() + System.DateTime.Now.Day.ToString() + System.DateTime.Now.Hour.ToString() + System.DateTime.Now.Minute.ToString() + System.DateTime.Now.Second.ToString();

		BackupPath = EnvPaths.GetBackupPath();
		if (string.IsNullOrEmpty(BackupPath))
		{
			BackupPath = Application.dataPath + "/MapsBackup/";
#if UNITY_EDITOR
			BackupPath = BackupPath.Replace("Assets/", "");
#endif
			//BackupPath = FolderParentPath;
		}

		BackupPath += FolderName + "/Backup_" + BackupId;

		if (BackupFiles && !System.IO.Directory.Exists(BackupPath))
			System.IO.Directory.CreateDirectory(BackupPath);
	}

	public void SaveScmap()
	{

		string MapFilePath = MapRelativePath(ScenarioLuaFile.Data.map);

		string FileName = ScenarioLuaFile.Data.map;
		char[] NameSeparator = ("/").ToCharArray();
		string[] Names = FileName.Split(NameSeparator);
		//Debug.Log(BackupPath + "/" + Names[Names.Length - 1]);
		if (BackupFiles && System.IO.File.Exists(MapFilePath))
			System.IO.File.Move(MapFilePath, BackupPath + "/" + Names[Names.Length - 1]);


		HeightmapControler.SaveScmapFile();
	}

	public void SaveTablesLua()
	{
		string TablesFilePath = LoadedMapFolderPath + ScenarioFileName + ".lua";
		TablesFilePath = TablesLua.ScenarioToTableFileName(TablesFilePath);
		//Debug.Log(TablesFilePath);
		if (BackupFiles && System.IO.File.Exists(TablesFilePath))
			System.IO.File.Move(TablesFilePath, TablesLua.ScenarioToTableFileName(BackupPath + "/" + ScenarioFileName + ".lua"));
		TablesLuaFile.Save(TablesFilePath);
	}

	public void SaveOptionsLua()
	{
		
		string OptionsFilePath = LoadedMapFolderPath + ScenarioFileName + ".lua";
		OptionsFilePath = TablesLua.ScenarioToOptionsFileName(OptionsFilePath);
		if (BackupFiles && System.IO.File.Exists(OptionsFilePath))
			System.IO.File.Move(OptionsFilePath, TablesLua.ScenarioToTableFileName(BackupPath + "/" + ScenarioFileName + ".lua"));

		System.IO.File.WriteAllText(OptionsFilePath, AdaptiveOptions.text);

	}

	public void SaveScriptLua(int ID = 0, bool NewBackup = false)
	{
		string SaveData = "";

		switch (ID)
		{
			case 1:
				string TablesFilePath = FolderName + "/" + ScenarioFileName + ".lua\"";
				TablesFilePath = TablesLua.ScenarioToTableFileName(TablesFilePath);

				SaveData = AdaptiveScript.text.Replace("[**TablesPath**]", "\"/maps/" + TablesFilePath);

				break;
			default:
				SaveData = DefaultScript.text;
				break;
		}

		if (NewBackup)
		{
			GenerateBackupPath();
		}

		string SaveFilePath = MapRelativePath(ScenarioLuaFile.Data.script);

		string FileName = ScenarioLuaFile.Data.script;
		char[] NameSeparator = ("/").ToCharArray();
		string[] Names = FileName.Split(NameSeparator);

		Debug.Log(SaveFilePath);
		Debug.Log(BackupPath + "/" + Names[Names.Length - 1]);

		if (BackupFiles && System.IO.File.Exists(SaveFilePath))
			System.IO.File.Move(SaveFilePath, BackupPath + "/" + Names[Names.Length - 1]);

		System.IO.File.WriteAllText(SaveFilePath, SaveData);
	}



	#endregion


	#region Map functions
	public void SortArmys()
	{

	}

	public Rect GetAreaRect(bool Round = false)
	{
		if (SaveLuaFile.Data.areas.Length > 0 && !AreaInfo.HideArea)
		{
			//int bigestAreaId = 0;
			Rect bigestAreaRect = new Rect(SaveLuaFile.Data.areas[0].rectangle);

			if (AreaInfo.SelectedArea != null)
			{
				bigestAreaRect = AreaInfo.SelectedArea.rectangle;
			}
			else
			{
				for (int i = 1; i < SaveLuaFile.Data.areas.Length; i++)
				{
					if (bigestAreaRect.x > SaveLuaFile.Data.areas[i].rectangle.x)
						bigestAreaRect.x = SaveLuaFile.Data.areas[i].rectangle.x;

					if (bigestAreaRect.y > SaveLuaFile.Data.areas[i].rectangle.y)
						bigestAreaRect.y = SaveLuaFile.Data.areas[i].rectangle.y;

					if (bigestAreaRect.width < SaveLuaFile.Data.areas[i].rectangle.width)
						bigestAreaRect.width = SaveLuaFile.Data.areas[i].rectangle.width;

					if (bigestAreaRect.height < SaveLuaFile.Data.areas[i].rectangle.height)
						bigestAreaRect.height = SaveLuaFile.Data.areas[i].rectangle.height;
				}
			}

			if (Round)
			{
				bigestAreaRect.x -= bigestAreaRect.x % 4;
				bigestAreaRect.width -= bigestAreaRect.width % 4;

				bigestAreaRect.y -= bigestAreaRect.y % 4;
				bigestAreaRect.height -= bigestAreaRect.height % 4;

			}

			float LastY = bigestAreaRect.y;
			bigestAreaRect.y = ScmapEditor.Current.map.Width - bigestAreaRect.height;
			bigestAreaRect.height = ScmapEditor.Current.map.Width - LastY;
			return bigestAreaRect;
		}
		else
		{

			return new Rect(0, 0, ScmapEditor.Current.map.Width, ScmapEditor.Current.map.Height);
		}

	}


	static Bounds PlayableAreaBounds = new Bounds();
	void SetBounds(Rect CurrentArea)
	{
		Vector3 Point0 = CurrentArea.min * 0.1f;
		Vector3 Point1 = CurrentArea.max * 0.1f;

		Point0.z = -Point1.y;
		Point1.z = -Point0.y;

		Point0.y = -0.1f;
		Point1.y = 26f;

		//Debug.Log(Point0);
		//Debug.Log(Point1);

		PlayableAreaBounds.min = Point0;
		PlayableAreaBounds.max = Point1;
	}

	void ClearBounds()
	{
		PlayableAreaBounds.center = MapCenterPoint;
		PlayableAreaBounds.size = new Vector3(ScenarioLuaFile.Data.Size[0] * 0.1f, 26, ScenarioLuaFile.Data.Size[1]);
	}

	public static bool IsInArea(Vector3 point)
	{
		return PlayableAreaBounds.Contains(point);
	}

	public void UpdateArea()
	{
		UpdateArea(LastRounding);

	}

	bool LastRounding = true;
	public void UpdateArea(bool Round)
	{
		LastRounding = Round;
		ClearBounds();
		if (SaveLuaFile.Data.areas.Length > 0 && !AreaInfo.HideArea)
		{
			//int bigestAreaId = 0;
			Rect bigestAreaRect = GetAreaRect(Round);


			if (bigestAreaRect.width > 0 && bigestAreaRect.height > 0)
			{

				// Set shaders
				Shader.SetGlobalInt("_Area", 1);
				Shader.SetGlobalVector("_AreaRect", new Vector4(bigestAreaRect.x / 10f, bigestAreaRect.y / 10f, bigestAreaRect.width / 10f, bigestAreaRect.height / 10f));
				SetBounds(bigestAreaRect);
			}
			else
			{
				Shader.SetGlobalInt("_Area", 0);
			}

		}
		else
		{
			Shader.SetGlobalInt("_Area", 0);
		}
	}
	#endregion



	#region Lua values
	public static Vector2 GetMapSize()
	{
		return new Vector2(Current.ScenarioLuaFile.Data.Size[0], Current.ScenarioLuaFile.Data.Size[1]);
	}

	public static float GetMapSizeX()
	{
		return Current.ScenarioLuaFile.Data.Size[0];
	}

	public static float GetMapSizeY()
	{
		return Current.ScenarioLuaFile.Data.Size[1];
	}

	public static string MapRelativePath(string luaPath)
	{
		return luaPath.Replace("/maps/", Current.FolderParentPath);
	}

	#endregion
}
