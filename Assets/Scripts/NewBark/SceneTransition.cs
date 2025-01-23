using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NewBark {
    public static class SceneTransition {
        private static bool canInteract = true;
        private static List<GameObject> lastState = new();

        private static float prevVolume;
        private static GameObject currentCamera;
        private static GameObject ui;
        private static GameObject player;
        
        public static IEnumerator LoadBattleScene() {
            if (!canInteract) yield break;
            canInteract = false;
            currentCamera = GameObject.Find("MainCamera");
            ui = GameObject.Find("UI");
            if (ui != null) {
                ui.SetActive(false);
            }

            prevVolume = GameManager.Audio.bgmVolume;
            player = GameObject.Find("Player");
            GameManager.Audio.BgmChannel.volume = 0.0f;
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);

            // Wait until the new scene is fully loaded
            while (!asyncLoad.isDone)
            {
                Debug.Log("Not done loading scene");
                yield return null;
            }
            Debug.Log("done loading scene");

            // Find the camera in the new scene and activate it
            
            GameObject newSceneCamera = GameObject.Find("Battle Camera");

            while (SC_GameLogic.transitioning) {
                Debug.Log("Waiting for transition");
                yield return new WaitForSeconds(0.5f);
            }
            Debug.Log("Done Transitioning");
            player.SetActive(false);
            
            lastState.Clear();
            
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.activeSelf) lastState.Add(go);
                Debug.Log(go.name);
                go.SetActive(false);
            }
            
            currentCamera.SetActive(false);

            if (newSceneCamera != null)
            {
                newSceneCamera.SetActive(true);
            }
        }

        public static void JumpBackToGameScene() {
            foreach (GameObject go in lastState)
            {
                go.SetActive(true);
            }
            
            currentCamera.SetActive(true);
            player.SetActive(true);
            ui.SetActive(true);
            canInteract = true;
            GameManager.Audio.BgmChannel.volume = prevVolume;
        }
    }
}