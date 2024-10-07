using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using Newtonsoft.Json;
using UnityEngine.UI;

public class AnswerSmashScript : MonoBehaviour {

    class ktaneData
    {
        public List<Dictionary<string, object>> KtaneModules { get; set; }
    }

    public KMAudio audio;
    public KMBombInfo bomb;
    public KMSelectable[] buttons;
    public Text[] displays;
    public MeshRenderer[] selectionOutlines;

    public GameObject[] bulbs;
    public Light[] bulbLights;

    public Light spotlight;

    public Material[] bulbStates;

    List<RepoEntry> entries = new List<RepoEntry>();
    KeyCode[] keyboardKeys = { KeyCode.BackQuote, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus, KeyCode.Equals, KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P, KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Backslash, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Semicolon, KeyCode.Quote, KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B, KeyCode.N, KeyCode.M, KeyCode.Comma, KeyCode.Period, KeyCode.Slash, KeyCode.Space };
    string typableCharacters = "`1234567890-=qwertyuiop[]\\asdfghjkl;'zxcvbnm,./ ";
    string typableCharactersShift = "~!@#$%^&*()_+QWERTYUIOP{}|ASDFGHJKL:\"ZXCVBNM<>? ";
    string generatedSmash;
    bool shiftDown;
    bool failedToGen;
    bool focused;
    bool loading = true;
    bool subButton = false;

    int cyclePos = 0;
    int selectedDisp;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable pressed = obj;
            pressed.OnInteract += delegate () { StartCoroutine(PressButton(pressed)); return false; };
        }
        if (Application.isEditor)
            focused = true;
        GetComponent<KMSelectable>().OnFocus += delegate () { focused = true; };
        GetComponent<KMSelectable>().OnDefocus += delegate () { focused = false; };
    }

    void Start()
    {
        StartCoroutine(LoadContentAndGenerate());
        StartCoroutine(lightCycle());
    }

    void Update()
    {
        if (moduleSolved != true && loading != true && focused != false)
        {
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                shiftDown = true;
            else if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
                shiftDown = false;
            if (Input.GetKeyDown(KeyCode.Backspace) && displays[selectedDisp + 1].text.Length > 0)
                displays[selectedDisp + 1].text = displays[selectedDisp + 1].text.Remove(displays[selectedDisp + 1].text.Length - 1, 1);
            for (int i = 0; i < keyboardKeys.Length; i++)
            {
                if (Input.GetKeyDown(keyboardKeys[i]))
                {
                    if (shiftDown)
                        displays[selectedDisp + 1].text += typableCharactersShift[i];
                    else
                        displays[selectedDisp + 1].text += typableCharacters[i];
                }
            }
            if (selectedDisp == 0)
            {
                selectionOutlines[0].enabled = true;
                selectionOutlines[1].enabled = false;
            }
            else
            {
                selectionOutlines[0].enabled = false;
                selectionOutlines[1].enabled = true;
            }
        }
    }

    IEnumerator PressButton(KMSelectable pressed)
    {
        if (moduleSolved != true && loading != true)
        {
            pressed.AddInteractionPunch();
            audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, pressed.transform);
            int index = Array.IndexOf(buttons, pressed);
            
            if (index == 0)
                selectedDisp = selectedDisp == 1 ? 0 : 1;
            else if (index == 1)
            {
                subButton = true;
                if (failedToGen)
                {
                    moduleSolved = true;
                    selectionOutlines[0].enabled = false;
                    selectionOutlines[1].enabled = false;
                    Debug.LogFormat("[Answer Smash #{0}] Module solved!", moduleId);
                    audio.PlaySoundAtTransform("DieSolveSound", transform);
                    GetComponent<KMBombModule>().HandlePass();
                }
                else
                {
                    Debug.LogFormat("[Answer Smash #{0}] Submitted {1} & {2}", moduleId, displays[1].text, displays[2].text);
                    List<string> smashes1 = GetSmashedAnswers(displays[1].text, displays[2].text);
                    List<string> smashes2 = GetSmashedAnswers(displays[2].text, displays[1].text);
                    if (displays[1].text == "" || displays[2].text == "")
                    {
                        Debug.LogFormat("[Answer Smash #{0}] Your answers cannot be blank, strike!", moduleId);
                        audio.PlaySoundAtTransform("SkipToDie", transform);
                        yield return new WaitForSeconds(4.0f);
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        GetComponent<KMBombModule>().HandleStrike();
                    }
                    else if (entries.All(x => x.Name != displays[1].text))
                    {                        
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        Debug.LogFormat("[Answer Smash #{0}] {1} is not a valid repository entry, strike!", moduleId, displays[1].text);
                        audio.PlaySoundAtTransform("CouldntResist", transform);
                        GetComponent<KMBombModule>().HandleStrike();
                    }
                    else if (entries.All(x => x.Name != displays[2].text))
                    {                        
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        Debug.LogFormat("[Answer Smash #{0}] {1} is not a valid repository entry, strike!", moduleId, displays[2].text);
                        audio.PlaySoundAtTransform("CouldntResist", transform);
                        GetComponent<KMBombModule>().HandleStrike();
                    }
                    else if (displays[1].text == displays[2].text)
                    {                        
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        Debug.LogFormat("[Answer Smash #{0}] Your answers cannot be the same repository entry, strike!", moduleId);
                        audio.PlaySoundAtTransform("CouldntResist", transform);
                        GetComponent<KMBombModule>().HandleStrike();
                    }
                    else if (smashes1.Contains(generatedSmash) || smashes2.Contains(generatedSmash))
                    {                        
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        moduleSolved = true;
                        selectionOutlines[0].enabled = false;
                        selectionOutlines[1].enabled = false;
                        Debug.LogFormat("[Answer Smash #{0}] Module solved!", moduleId);
                        audio.PlaySoundAtTransform("DieSolveSound", transform);
                        GetComponent<KMBombModule>().HandlePass();
                    }
                    else
                    {
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        Debug.LogFormat("[Answer Smash #{0}] Your answers cannot make the displayed answer smash, strike!", moduleId);
                        audio.PlaySoundAtTransform("CouldntResist", transform);
                        GetComponent<KMBombModule>().HandleStrike();
                    }
                }
            }
        }
    }

    IEnumerator lightCycle(){
        for (int i = 0; i < bulbLights.Length; i++){
            bulbLights[i].enabled = false;
            bulbs[i].GetComponent<MeshRenderer>().material = bulbStates[0];
        }

        while (subButton == false){
            for (int i = 0; i < bulbLights.Length; i++)
            {
                if(cyclePos == 0){
                    bulbLights[i].enabled = (i % 3 == 0);
                    bulbs[i].GetComponent<MeshRenderer>().material = bulbStates[1];
                    for (int j = 1; j < bulbLights.Length; j += 3){
                        bulbLights[j].enabled = false;
                        bulbs[j].GetComponent<MeshRenderer>().material = bulbStates[0];
                    }
                    for (int k = 2; k < bulbLights.Length; k += 3){
                        bulbLights[k].enabled = false;
                        bulbs[k].GetComponent<MeshRenderer>().material = bulbStates[0];
                    }
                }
                else if (cyclePos == 1){
                    bulbLights[i].enabled = (i % 3 == 1);
                    bulbs[i].GetComponent<MeshRenderer>().material = bulbStates[1];
                    for (int j = 0; j < bulbLights.Length; j += 3)
                    {
                        bulbLights[j].enabled = false;
                        bulbs[j].GetComponent<MeshRenderer>().material = bulbStates[0];
                    }
                    for (int k = 2; k < bulbLights.Length; k += 3)
                    {
                        bulbLights[k].enabled = false;
                        bulbs[k].GetComponent<MeshRenderer>().material = bulbStates[0];
                    }
                }
                else{
                    bulbLights[i].enabled = (i % 3 == 2);
                    bulbs[i].GetComponent<MeshRenderer>().material = bulbStates[1];
                    for (int j = 0; j < bulbLights.Length; j += 3)
                    {
                        bulbLights[j].enabled = false;
                        bulbs[j].GetComponent<MeshRenderer>().material = bulbStates[0];
                    }
                    for (int k = 1; k < bulbLights.Length; k += 3)
                    {
                        bulbLights[k].enabled = false;
                        bulbs[k].GetComponent<MeshRenderer>().material = bulbStates[0];
                    }
                }
            }
            yield return new WaitForSeconds(0.15f);
            cyclePos += 2;
            cyclePos = cyclePos % 3;

        }            
        for (int i = 0; i < bulbLights.Length; i++)
        {
            bulbLights[i].enabled = false;
            bulbs[i].GetComponent<MeshRenderer>().material = bulbStates[0];
        }
    }

    IEnumerator submissionAnim(){
        audio.PlaySoundAtTransform("Lights_Camera_Action", transform);
        yield return new WaitForSeconds(1.0f);
        //SceneManager.Instance.GameplayState.Room.CeilingLight.TurnOff(false);
        yield return new WaitForSeconds(2.2f);
        spotlight.enabled = true;
        yield return new WaitForSeconds(3.3f);
        //SceneManager.Instance.GameplayState.Room.CeilingLight.TurnOff(true);
        spotlight.enabled = false;
    }

    List<string> GetSmashedAnswers(string ans1, string ans2)
    {
        List<string> smashes = new List<string>();
        for (int i = 1; i < ans1.Length; i++)
        {
            for (int j = 0; j < ans2.Length - 1; j++)
            {
                if (ans1[i] == ans2[j])
                    smashes.Add(ans1.Substring(0, i) + ans2.Substring(j));
            }
        }
        return smashes.Distinct().ToList();
    }

    List<RepoEntry> ProcessJson(string fetched)
    {
        ktaneData Deserialized = JsonConvert.DeserializeObject<ktaneData>(fetched);
        List<RepoEntry> RepoEntries = new List<RepoEntry>();
        foreach (var item in Deserialized.KtaneModules)
            RepoEntries.Add(new RepoEntry(item));
        return RepoEntries;
    }

    IEnumerator LoadContentAndGenerate()
    {
        WWW fetch = new WWW("https://ktane.timwi.de/json/raw");
        yield return fetch;
        if (fetch.error == null)
        {
            entries = ProcessJson(fetch.text);
            regen:
            int choice1 = UnityEngine.Random.Range(0, entries.Count);
            int choice2 = UnityEngine.Random.Range(0, entries.Count);
            while (choice1 == choice2)
                choice2 = UnityEngine.Random.Range(0, entries.Count);
            List<string> allSmashes = GetSmashedAnswers(entries[choice1].Name, entries[choice2].Name);
            string allTypableCharacters = typableCharacters + typableCharactersShift;
            if (allSmashes.Count == 0 || entries[choice1].Name.Any(x => !allTypableCharacters.Contains(x)) || entries[choice2].Name.Any(x => !allTypableCharacters.Contains(x)))
                goto regen;
            generatedSmash = allSmashes.PickRandom();
            displays[0].text = generatedSmash;
            Debug.LogFormat("[Answer Smash #{0}] The answer smash is {1}", moduleId, generatedSmash);
            Debug.LogFormat("[Answer Smash #{0}] One possible solution is {1} & {2}", moduleId, entries[choice1].Name, entries[choice2].Name);
        }
        else
        {
            failedToGen = true;
            Debug.LogFormat("[Answer Smash #{0}] Error: Failed to connect to the repository of manual pages", moduleId);
            Debug.LogFormat("[Answer Smash #{0}] Press the submit button to solve the module", moduleId);
            StartCoroutine(FailConnectFlash());
        }
        loading = false;
    }

    IEnumerator FailConnectFlash()
    {
        while (true)
        {
            displays[0].text = "Error";
            yield return new WaitForSeconds(1);
            displays[0].text = "";
            yield return new WaitForSeconds(1);
        }
    }
}