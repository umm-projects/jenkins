﻿using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityModule.Settings;

namespace ContinuousIntegration {
    public partial class Jenkins {

        /// <summary>
        /// ジョブ種別
        /// </summary>
        public enum JobType {
            // Player パッケージ
            Player,
            // AssetBundle
            AssetBundle,
        }

        /// <summary>
        /// git コマンドパス
        /// </summary>
        private const string COMMAND_PATH_GIT = "/usr/local/bin/git";

        /// <summary>
        /// カレントのリポジトリ名称を取得するためのパラメータ
        /// </summary>
        private const string ARGUMENTS_CURRENT_REPOSITORY_NAME = "remote get-url origin";

        /// <summary>
        /// カレントのブランチ名称を取得するためのパラメータ
        /// </summary>
        private const string ARGUMENTS_CURRENT_BRANCH_NAME = "rev-parse --abbrev-ref HEAD";

        /// <summary>
        /// Jenkins のジョブ名称マップ
        /// </summary>
        private static readonly Dictionary<JobType, string> JOB_NAME_MAP = new Dictionary<JobType, string>();

        /// <summary>
        /// Jenkins にビルドリクエストを発行する
        /// </summary>
        /// <param name="jobType">ジョブ種別</param>
        /// <param name="buildTarget">ビルドターゲット</param>
        public static void SendBuildRequest(JobType jobType, BuildTarget buildTarget) {
            if (!IsValid(jobType)) {
                return;
            }
            ObservableUnityWebRequest.Post(Path.Combine(Path.Combine(JenkinsSetting.GetOrDefault().BaseURL, JOB_NAME_MAP[jobType]), "buildWithParameters"), GenerateParameters(buildTarget), GenerateRequestHeader()).Subscribe(
                (_) => {
                    Debug.Log("Build request sent to Jenkins.");
                },
                (ex) => {
                    Debug.Log("Could not send build request to Jenkins: " + ex.Message);
                }
            );
        }

        /// <summary>
        /// Jenkins にビルドリクエストを発行可能かどうか
        /// </summary>
        /// <param name="jobType">ジョブ種別</param>
        /// <returns>true: 発行可能 / false: 発行不可能</returns>
        private static bool IsValid(JobType jobType) {
            bool result = true;
            if (string.IsNullOrEmpty(JenkinsSetting.GetOrDefault().SlackUserName)) {
                Debug.LogError("Slack のユーザ名称を設定してください。");
                result = false;
            }
            if (string.IsNullOrEmpty(JenkinsSetting.GetOrDefault().UserId)) {
                Debug.LogError("Jenkins のユーザ ID を設定してください。");
                result = false;
            }
            if (string.IsNullOrEmpty(JenkinsSetting.GetOrDefault().Password)) {
                Debug.LogError("Jenkins のパスワードを設定してください。");
                result = false;
            }
            if (jobType == JobType.Player && string.IsNullOrEmpty(JenkinsSetting.GetOrDefault().JobNameForPlayer)) {
                Debug.LogError("Jenkins の Player ビルド用ジョブ名称を設定してください。");
                result = false;
            }
            if (jobType == JobType.AssetBundle && string.IsNullOrEmpty(JenkinsSetting.GetOrDefault().JobNameForAssetBunde)) {
                Debug.LogError("Jenkins の AssetBundle ビルド用ジョブ名称を設定してください。");
                result = false;
            }
            if (result) {
                JOB_NAME_MAP[JobType.Player] = JenkinsSetting.GetOrDefault().JobNameForPlayer;
                JOB_NAME_MAP[JobType.AssetBundle] = JenkinsSetting.GetOrDefault().JobNameForAssetBunde;
            }
            return result;
        }

        /// <summary>
        /// Jenkins に渡すパラメータを生成する
        /// </summary>
        /// <param name="buildTarget">ビルドターゲット</param>
        /// <returns>Jenkins に渡すパラメータ</returns>
        private static Dictionary<string, string> GenerateParameters(BuildTarget buildTarget) {
            return new Dictionary<string, string>() {
                { "requested_user"   , JenkinsSetting.GetOrDefault().SlackUserName },
                { "repository"       , GetCurrentRepositoryName() },
                { "branch"           , GetCurrentBranchName() },
                { "platform"         , buildTarget.ToString() },
                { "editor_version"   , Application.unityVersion },
                { "development_build", EditorUserBuildSettings.development.ToString() },
            };
        }

        /// <summary>
        /// Jenkins に渡すリクエストヘッダを生成する
        /// </summary>
        /// <returns>Jenkins に渡すリクエストヘッダ</returns>
        private static Dictionary<string, string> GenerateRequestHeader() {
            return new Dictionary<string, string>() {
                { "Authorization", "Basic " + System.Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(string.Format("{0}:{1}", JenkinsSetting.GetOrDefault().UserId, JenkinsSetting.GetOrDefault().Password))) },
            };
        }

        /// <summary>
        /// カレントのリポジトリ名称を取得する
        /// </summary>
        /// <returns>カレントのリポジトリ名称</returns>
        private static string GetCurrentRepositoryName() {
            System.Diagnostics.Process process = new System.Diagnostics.Process {
                StartInfo = {
                    FileName = COMMAND_PATH_GIT,
                    Arguments = ARGUMENTS_CURRENT_REPOSITORY_NAME,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };
            process.Start();
            process.WaitForExit();
            string remoteURL = process.StandardOutput.ReadToEnd().TrimEnd();
            Match match = Regex.Match(remoteURL, "^[^/]+/([^.]+)\\.git$");
            string currentRepositoryName = match.Groups[1].Value;
            process.Close();
            return currentRepositoryName;
        }

        /// <summary>
        /// カレントのブランチ名称を取得する
        /// </summary>
        /// <returns>カレントのブランチ名称</returns>
        private static string GetCurrentBranchName() {
            System.Diagnostics.Process process = new System.Diagnostics.Process {
                StartInfo = {
                    FileName = COMMAND_PATH_GIT,
                    Arguments = ARGUMENTS_CURRENT_BRANCH_NAME,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                    }
                };
            process.Start();
            process.WaitForExit();
            string currentBranchName = process.StandardOutput.ReadToEnd().TrimEnd();
            process.Close();
            return currentBranchName;
        }
    }
}
