﻿using System;
using System.IO;
using System.IO.Abstractions;
using System.Windows;
using YamlDotNet.Serialization;

namespace GW2_Addon_Manager
{
    /// <summary>
    /// The <c>configuration</c> class contains various functions dealing with application configuration. 
    /// </summary>
    public class Configuration
    {
        static readonly string config_file_path = "config.yaml";
        static readonly string config_template_path = "resources\\config_template.yaml";
        static readonly string applicationRepoUrl = "https://api.github.com/repos/fmmmlee/GW2-Addon-Manager/releases/latest";

        /**** For Testing Purposes ****/
        public static IFileSystem fileSys = new FileSystem();

        /// <summary>
        /// Sets the file system to be used to <paramref name="mock"/> for testing.
        /// </summary>
        /// <param name="mock"></param>
        public static void AttachMockFileSystem(IFileSystem mock)
        {
            fileSys = mock;
        }

        /****  ****/

        /// <summary>
        /// Reads config.yaml
        /// </summary>
        /// <returns></returns>
        public static UserConfig getConfigAsYAML()
        {
            string updateFile;
            if (fileSys.File.Exists(config_file_path))
                updateFile = fileSys.File.ReadAllText(config_file_path);
            else
                updateFile = fileSys.File.ReadAllText(config_template_path);

            Deserializer toDynamic = new Deserializer();
            UserConfig user_config = toDynamic.Deserialize<UserConfig>(updateFile);
            return user_config;
        }

        /// <summary>
        /// Write the config.yaml file in the application folder
        /// </summary>
        /// <param name="info"></param>
        public static void setConfigAsYAML(UserConfig info)
        {
            String config_file_path = "config.yaml";
            fileSys.File.WriteAllText(config_file_path, new Serializer().Serialize(info));
        }

        /// <summary>
        /// Writes the config-template.yaml file in the /resources folder (not in use, will keep for possible use in future)
        /// </summary>
        /// <param name="info"></param>
        public static void setTemplateYAML(UserConfig info)
        {
            fileSys.File.WriteAllText(config_template_path, new Serializer().Serialize(info));
        }

        /// <summary>
        /// <c>getTemplateConfig</c> accesses the configuration file template found at <c>config_template_path</c>
        /// </summary>
        /// <returns> a config object representing the default configuration file template as serialized from YAML </returns>
        public static UserConfig getTemplateConfig()
        {
            String config_template_path = "resources\\config_template.yaml";

            string updateFile = fileSys.File.ReadAllText(config_template_path);

            Deserializer toDynamic = new Deserializer();
            UserConfig user_config = toDynamic.Deserialize<UserConfig>(updateFile);
            return user_config;
        }

        /// <summary>
        /// Displays the latest status of the plugins on the opening screen (disabled, enabled, version, installed).
        /// </summary>
        public static void DisplayAddonStatus()
        {
            UserConfig config_obj = getConfigAsYAML();

            foreach(AddonInfoFromYaml addon in OpeningViewModel.GetInstance.AddonList)
            {
                addon.addon_name = AddonYamlReader.getAddonInInfo(addon.folder_name).addon_name;
                if (config_obj.installed.ContainsKey(addon.folder_name) && config_obj.version.ContainsKey(addon.folder_name))
                {
                    if (addon.folder_name == "arcdps" || addon.folder_name == "buildPad" || config_obj.version[addon.folder_name].Length > 10)
                        addon.addon_name += " (installed)";
                    else
                        addon.addon_name += " (" + config_obj.version[addon.folder_name] + " installed)";   
                }

                if (config_obj.disabled.ContainsKey(addon.folder_name) && config_obj.disabled[addon.folder_name] == true)
                        addon.addon_name += " (disabled)";
            }
        }

        /// <summary>
        /// <c>ChangeAddonConfig</c> writes the default add-ons section of the configuration file found at <c>config_file_path</c> using
        /// values found in the OpeningViewModel, which can be set by the user.
        /// </summary>
        public static void ChangeAddonConfig()
        {
            UserConfig config_obj = getConfigAsYAML();
            foreach(AddonInfoFromYaml addon in OpeningViewModel.GetInstance.AddonList)
            {
                if (addon.IsSelected)
                {
                    if (config_obj.default_configuration.ContainsKey(addon.folder_name))
                        config_obj.default_configuration[addon.folder_name] = true;
                    else
                        config_obj.default_configuration.Add(addon.folder_name, true);
                }
                else
                {
                    if (config_obj.default_configuration.ContainsKey(addon.folder_name))
                        config_obj.default_configuration[addon.folder_name] = false;
                }
                
            }
            setConfigAsYAML(config_obj);
        }

        /// <summary>
        /// <c>SetGamePath</c> both sets the game path for the current application session to <paramref name="path"/> and records it in the configuration file.
        /// </summary>
        /// <param name="path">The game path.</param>
        public static void SetGamePath(string path)
        {
            try
            {
                Application.Current.Properties["game_path"] = path.Replace("\\", "\\\\");
            }
            catch (Exception)
            { }
            
            UserConfig config_obj = getConfigAsYAML();
            config_obj.game_path = path;
            setConfigAsYAML(config_obj);
            DetermineSystemType();
        }

        /// <summary>
        /// Checks if there is a new version of the application available.
        /// </summary>
        public static void CheckSelfUpdates()
        {
            UserConfig currentConfig = getConfigAsYAML();
            UserConfig templateConfig = getTemplateConfig();
            if (currentConfig.application_version != templateConfig.application_version)
            {
                currentConfig.application_version = templateConfig.application_version;
                setConfigAsYAML(currentConfig);
            }

            string thisVersion = getConfigAsYAML().application_version;
            string latestVersion;

            dynamic release_info = UpdateHelpers.GitReleaseInfo(applicationRepoUrl);
            latestVersion = release_info.tag_name;

            if (latestVersion != thisVersion)
            {
                OpeningViewModel.GetInstance.UpdateAvailable = latestVersion + " available!";
                OpeningViewModel.GetInstance.UpdateLinkVisibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Attempts to read the game folder and determine whether the game is running on a 64 or 32-bit system.
        /// Based on that, sets the 'bin_folder' property in the configuration file.
        /// </summary>
        public static void DetermineSystemType()
        {
            UserConfig config = getConfigAsYAML();
            if (fileSys.Directory.Exists(config.game_path.ToString()))
            {
                if (fileSys.Directory.Exists(config.game_path.ToString() + "\\bin64"))
                {
                    config.bin_folder = "bin64";
                    config.exe_name = "Gw2-64.exe";
                }
                else if (fileSys.Directory.Exists(config.game_path.ToString() + "\\bin"))
                {
                    config.bin_folder = "bin";
                    config.exe_name = "Gw2.exe";
                }
                setConfigAsYAML(config);
            }
        }

        /// <summary>
        /// Deletes all addons, addon loader, and configuration data related to addons.
        /// </summary>
        public static void DeleteAllAddons()
        {
            //get user config
            UserConfig config = getConfigAsYAML();
            UserConfig templateConfig = getTemplateConfig();
            //set installed, disabled, default, and version collections to the default installation setting
            config.installed = templateConfig.installed;
            config.disabled = templateConfig.disabled;
            config.version = templateConfig.version;
            config.default_configuration = templateConfig.default_configuration;

            //clear loader_version
            config.loader_version = null;

            //delete disabled plugins folder: ${install dir}/disabled plugins
            if(Directory.Exists("Disabled Plugins"))
                Directory.Delete("Disabled Plugins", true);
            //delete addons: {game folder}/addons
            if(Directory.Exists(Path.Combine(config.game_path.ToString(), "addons")))
                Directory.Delete(Path.Combine(config.game_path.ToString(), "addons"), true);
            //delete addon loader: {game folder}/{bin/64}/d3d9.dll
            File.Delete(Path.Combine(Path.Combine(config.game_path.ToString(), config.bin_folder.ToString()), "d3d9.dll"));

            //write cleaned config file
            setConfigAsYAML(config);
        }
    }
}
