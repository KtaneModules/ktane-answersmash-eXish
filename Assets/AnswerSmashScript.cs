using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using Newtonsoft.Json;
using UnityEngine.UI;
using Rnd = UnityEngine.Random;


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
    public GameObject[] allBulbs;
    public GameObject[] largeDisplays;
    public Light[] bulbLights;
    public Light flashbang;

    public Light spotlight;

    public Material[] bulbStates;

    List<RepoEntry> entries = new List<RepoEntry>();
    KeyCode[] keyboardKeys = { KeyCode.BackQuote, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus, KeyCode.Equals, KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P, KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Backslash, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Semicolon, KeyCode.Quote, KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B, KeyCode.N, KeyCode.M, KeyCode.Comma, KeyCode.Period, KeyCode.Slash, KeyCode.Space };
    string typableCharacters = "`1234567890-=qwertyuiop[]\\asdfghjkl;'zxcvbnm,./ ";
    string typableCharactersShift = "~!@#$%^&*()_+QWERTYUIOP{}|ASDFGHJKL:\"ZXCVBNM<>? ";
    string generatedSmash;
    string exampleSol1;
    string exampleSol2;
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

    void Start()
    {
        moduleId = moduleIdCounter++;
        float scalar = transform.lossyScale.x;
        for (var i = 0; i < bulbLights.Length; i++)
            bulbLights[i].range *= scalar;
        spotlight.range *= scalar;
        flashbang.range *= scalar;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable pressed = obj;
            pressed.OnInteract += delegate () { StartCoroutine(PressButton(pressed)); return false; };
        }
        GetComponent<KMSelectable>().OnFocus += delegate () { focused = true; };
        GetComponent<KMSelectable>().OnDefocus += delegate () { focused = false; selectionOutlines[0].enabled = false; selectionOutlines[1].enabled = false; };
        StartCoroutine(LoadContentAndGenerate());
        StartCoroutine(lightCycle());
    }

    void Update()
    {
        if (moduleSolved != true && loading != true && focused != false && subButton != true)
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
        if (moduleSolved != true && loading != true && subButton != true)
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
                        selectionOutlines[0].enabled = false;
                        selectionOutlines[1].enabled = false;
                        Debug.LogFormat("[Answer Smash #{0}] Your answers cannot be blank, strike!", moduleId);
                        audio.PlaySoundAtTransform("SkipToDie", transform); //Special little case I had in mind when making the model if one of the answers was blank (Crazycaleb).
                        yield return new WaitForSeconds(4.0f);
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        audio.PlaySoundAtTransform("CouldntResist", transform);
                        GetComponent<KMBombModule>().HandleStrike();
                        buttons[0].gameObject.SetActive(true);
                        buttons[1].gameObject.SetActive(true);
                        largeDisplays[0].SetActive(true);
                        displays[0].gameObject.SetActive(true);
                        displays[1].transform.localPosition = new Vector3(-0.015f, -0.0033f, -0.015f);
                        displays[2].transform.localPosition = new Vector3(-0.015f, -0.034f, -0.015f);
                        largeDisplays[1].transform.localPosition = new Vector3(-0.015f, 0.012f, -0.0033f);
                        largeDisplays[2].transform.localPosition = new Vector3(-0.015f, 0.012f, -0.034f);  
                        for (int i = 0; i < allBulbs.Length; i++)
                        {
                            allBulbs[i].SetActive(true);
                        }
                        subButton = false;
                        StartCoroutine(lightCycle());
                    }
                    else if (entries.All(x => x.Name != displays[1].text))
                    {
                        selectionOutlines[0].enabled = false;
                        selectionOutlines[1].enabled = false;
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        Debug.LogFormat("[Answer Smash #{0}] {1} is not a valid repository entry, strike!", moduleId, displays[1].text);
                        audio.PlaySoundAtTransform("CouldntResist", transform);
                        GetComponent<KMBombModule>().HandleStrike();
                        buttons[0].gameObject.SetActive(true);
                        buttons[1].gameObject.SetActive(true);
                        largeDisplays[0].SetActive(true);
                        displays[0].gameObject.SetActive(true);
                        displays[1].transform.localPosition = new Vector3(-0.015f, -0.0033f, -0.015f);
                        displays[2].transform.localPosition = new Vector3(-0.015f, -0.034f, -0.015f);
                        largeDisplays[1].transform.localPosition = new Vector3(-0.015f, 0.012f, -0.0033f);
                        largeDisplays[2].transform.localPosition = new Vector3(-0.015f, 0.012f, -0.034f);
                        for (int i = 0; i < allBulbs.Length; i++)
                        {
                            allBulbs[i].SetActive(true);
                        }
                        subButton = false;
                        StartCoroutine(lightCycle());
                    }
                    else if (entries.All(x => x.Name != displays[2].text))
                    {
                        selectionOutlines[0].enabled = false;
                        selectionOutlines[1].enabled = false;
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        Debug.LogFormat("[Answer Smash #{0}] {1} is not a valid repository entry, strike!", moduleId, displays[2].text);
                        audio.PlaySoundAtTransform("CouldntResist", transform);
                        GetComponent<KMBombModule>().HandleStrike();
                        largeDisplays[0].SetActive(true);
                        displays[0].gameObject.SetActive(true);
                        buttons[0].gameObject.SetActive(true);
                        buttons[1].gameObject.SetActive(true);
                        displays[1].transform.localPosition = new Vector3(-0.015f, -0.0033f, -0.015f);
                        displays[2].transform.localPosition = new Vector3(-0.015f, -0.034f, -0.015f);
                        largeDisplays[1].transform.localPosition = new Vector3(-0.015f, 0.012f, -0.0033f);
                        largeDisplays[2].transform.localPosition = new Vector3(-0.015f, 0.012f, -0.034f);
                        for (int i = 0; i < allBulbs.Length; i++)
                        {
                            allBulbs[i].SetActive(true);
                        }
                        subButton = false;
                        StartCoroutine(lightCycle());
                    }
                    else if (displays[1].text == displays[2].text)
                    {
                        selectionOutlines[0].enabled = false;
                        selectionOutlines[1].enabled = false;
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        Debug.LogFormat("[Answer Smash #{0}] Your answers cannot be the same repository entry, strike!", moduleId);
                        audio.PlaySoundAtTransform("CouldntResist", transform);
                        GetComponent<KMBombModule>().HandleStrike();
                        buttons[0].gameObject.SetActive(true);
                        buttons[1].gameObject.SetActive(true);
                        largeDisplays[0].SetActive(true);
                        displays[0].gameObject.SetActive(true);
                        displays[1].transform.localPosition = new Vector3(-0.015f, -0.0033f, -0.015f);
                        displays[2].transform.localPosition = new Vector3(-0.015f, -0.034f, -0.015f);
                        largeDisplays[1].transform.localPosition = new Vector3(-0.015f, 0.012f, -0.0033f);
                        largeDisplays[2].transform.localPosition = new Vector3(-0.015f, 0.012f, -0.034f);
                        for (int i = 0; i < allBulbs.Length; i++)
                        {
                            allBulbs[i].SetActive(true);
                        }
                        subButton = false;
                        StartCoroutine(lightCycle());
                    }
                    else if (smashes1.Contains(generatedSmash) || smashes2.Contains(generatedSmash))
                    {
                        selectionOutlines[0].enabled = false;
                        selectionOutlines[1].enabled = false;
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        moduleSolved = true;
                        Debug.LogFormat("[Answer Smash #{0}] Module solved!", moduleId);
                        audio.PlaySoundAtTransform("DieSolveSound", transform);
                        GetComponent<KMBombModule>().HandlePass();
                        largeDisplays[1].gameObject.SetActive(false);
                        largeDisplays[2].gameObject.SetActive(false);
                        displays[1].gameObject.SetActive(false);
                        displays[2].gameObject.SetActive(false);
                        largeDisplays[0].SetActive(true);
                        largeDisplays[0].transform.localPosition = new Vector3(0f, 0.012f, 0f);
                        displays[0].gameObject.SetActive(true);
                        displays[0].transform.localPosition = new Vector3(0f, 0f, -0.015f);
                        for (int i = 0; i < allBulbs.Length; i++)
                        {
                            allBulbs[i].SetActive(true);
                        }
                    }
                    else
                    {
                        selectionOutlines[0].enabled = false;
                        selectionOutlines[1].enabled = false;
                        StartCoroutine(submissionAnim());
                        yield return new WaitForSeconds(6.5f);
                        Debug.LogFormat("[Answer Smash #{0}] Your answers cannot make the displayed answer smash, strike!", moduleId);
                        audio.PlaySoundAtTransform("CouldntResist", transform);
                        GetComponent<KMBombModule>().HandleStrike();
                        largeDisplays[0].SetActive(true);
                        displays[0].gameObject.SetActive(true);
                        buttons[0].gameObject.SetActive(true);
                        buttons[1].gameObject.SetActive(true);
                        displays[1].transform.localPosition = new Vector3(-0.015f, -0.0033f, -0.015f);
                        displays[2].transform.localPosition = new Vector3(-0.015f, -0.034f, -0.015f);
                        largeDisplays[1].transform.localPosition = new Vector3(-0.015f, 0.012f, -0.0033f);
                        largeDisplays[2].transform.localPosition = new Vector3(-0.015f, 0.012f, -0.034f);
                        for (int i = 0; i < allBulbs.Length; i++)
                        {
                            allBulbs[i].SetActive(true);
                        }
                        subButton = false;
                        StartCoroutine(lightCycle());
                    }
                }
            }
        }
    }

    IEnumerator lightCycle(){
        flashbang.enabled = false;
        flashbang.intensity = 0;
        for (int i = 0; i < bulbLights.Length; i++){ //Start with everything off
            bulbLights[i].enabled = false;
            bulbs[i].GetComponent<MeshRenderer>().material = bulbStates[0];
        }

        while (subButton == false){ //If we haven't pressed submit button, do the light cycle
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
            cyclePos += 2; //So it'll rotate clockwise, instead of counter.
            cyclePos = cyclePos % 3; 

        }            
        for (int i = 0; i < bulbLights.Length; i++) //Once the submit button is pressed, turn the lights off.
        {
            bulbLights[i].enabled = false;
            bulbs[i].GetComponent<MeshRenderer>().material = bulbStates[0];
        }
    }

    IEnumerator submissionAnim(){
        audio.PlaySoundAtTransform("Lights_Camera_Action", transform);
        yield return new WaitForSeconds(1.5f);
        if (!Application.isEditor){ //If we aren't in Unity, turn lights off.
            SceneManager.Instance.GameplayState.Room.CeilingLight.TurnOff(false); 
        }
        for (int i = 0; i < allBulbs.Length; i++){ //Remove Bulbs during the animation while it is dark in the room
            allBulbs[i].SetActive(false);
        }
        largeDisplays[0].SetActive(false);
        displays[0].gameObject.SetActive(false);
        buttons[0].gameObject.SetActive(false);
        buttons[1].gameObject.SetActive(false);
        displays[1].transform.localPosition = new Vector3(0f, 0.03f, -0.015f);
        displays[2].transform.localPosition = new Vector3(0f, -0.03f, -0.015f);
        largeDisplays[1].transform.localPosition = new Vector3(0f, 0.012f, 0.03f);
        largeDisplays[2].transform.localPosition = new Vector3(0f, 0.012f, -0.03f);
        yield return new WaitForSeconds(1.7f);        
        spotlight.enabled = true; //Turn spotlight on
        StartCoroutine(shakeshakeshakeSenora());
        yield return new WaitForSeconds(3.3f);
        if (!Application.isEditor)//After submission, turn lights back on. (Out of the eternal darkness)
        {
            SceneManager.Instance.GameplayState.Room.CeilingLight.TurnOn(true);
        }
        spotlight.enabled = false; //Spotlight off
    }

    IEnumerator shakeshakeshakeSenora(){
        float elapsedTime = 0f;
        bool flashed = false;
        float duration = 3.2f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            displays[1].transform.localEulerAngles = new Vector3(0, Mathf.Sin(elapsedTime / (elapsedTime < 3 ? .1f : 3f) * Mathf.PI * 2) * 1f, 0);
            displays[2].transform.localEulerAngles = new Vector3(0, Mathf.Sin(elapsedTime / (elapsedTime < 3 ? .1f : 3f) * Mathf.PI * 2) * 1f, 0);
            largeDisplays[1].transform.localEulerAngles = new Vector3(0, Mathf.Sin(elapsedTime / (elapsedTime < 3 ? .1f : 3f) * Mathf.PI * 2) * 1f, 0);
            largeDisplays[2].transform.localEulerAngles = new Vector3(0, Mathf.Sin(elapsedTime / (elapsedTime < 3 ? .1f : 3f) * Mathf.PI * 2) * 1f, 0);
            displays[1].transform.localPosition = new Vector3(0f, Mathf.Lerp(0.03f, 0f, elapsedTime / duration), -0.015f);
            displays[2].transform.localPosition = new Vector3(0f, Mathf.Lerp(-0.03f, 0f, elapsedTime / duration), -0.015f);
            largeDisplays[1].transform.localPosition = new Vector3(0f, 0.012f, Mathf.Lerp(0.03f, 0f, elapsedTime / duration));
            largeDisplays[2].transform.localPosition = new Vector3(0f, 0.012f, Mathf.Lerp(-0.03f, 0f, elapsedTime / duration));
            if (elapsedTime >= 2.2f && !flashed){
                StartCoroutine(FLASHAHHAHHSAVIOROFTHEUNIVERSE());
                flashed = true;
            }
            yield return null;
        }
        displays[1].transform.localEulerAngles = new Vector3(0, 0, 0);
        displays[2].transform.localEulerAngles = new Vector3(0, 0, 0);
        largeDisplays[1].transform.localEulerAngles = new Vector3(0, 0, 0);
        largeDisplays[2].transform.localEulerAngles = new Vector3(0, 0, 0);
    }

    IEnumerator FLASHAHHAHHSAVIOROFTHEUNIVERSE()
    {
        flashbang.enabled = true;
        var duration = 1f;
        var elapsed = 0f;
        var flashSize = 20f;
        while (elapsed < duration)
        {
            flashbang.intensity = Easing.OutQuad(elapsed, 0f, flashSize, duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        // Do your code switch thing here
        elapsed = 0f;
        while (elapsed < duration)
        {
            flashbang.intensity = Easing.OutQuad(elapsed, flashSize, 0f, duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        flashbang.enabled = false;
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
            int choice1 = Rnd.Range(0, entries.Count);
            int choice2 = Rnd.Range(0, entries.Count);
            while (choice1 == choice2)
                choice2 = Rnd.Range(0, entries.Count);
            List<string> allSmashes = GetSmashedAnswers(entries[choice1].Name, entries[choice2].Name);
            string allTypableCharacters = typableCharacters + typableCharactersShift;
            if (allSmashes.Count == 0 || entries[choice1].Name.Any(x => !allTypableCharacters.Contains(x)) || entries[choice2].Name.Any(x => !allTypableCharacters.Contains(x)) || entries[choice1].Translation || entries[choice2].Translation)
                goto regen;
            exampleSol1 = entries[choice1].Name;
            exampleSol2 = entries[choice2].Name;
            generatedSmash = allSmashes.PickRandom();
            displays[0].text = generatedSmash;
            Debug.LogFormat("[Answer Smash #{0}] The answer smash is {1}", moduleId, generatedSmash);
            Debug.LogFormat("[Answer Smash #{0}] One possible solution is {1} & {2}", moduleId, exampleSol1, exampleSol2);
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

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} type <text> [Types the specified text] | !{0} clear [Clears the text on the selected display] | !{0} toggle [Presses the toggle button] | !{0} submit [Presses the submit button]";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if (command.EqualsIgnoreCase("toggle"))
        {
            if (subButton || loading)
            {
                yield return "sendtochaterror You can't press the toggle button right now!";
                yield break;
            }
            yield return null;
            buttons[0].OnInteract();
            yield break;
        }
        if (command.EqualsIgnoreCase("submit"))
        {
            if (subButton || loading)
            {
                yield return "sendtochaterror You can't press the submit button right now!";
                yield break;
            }
            yield return null;
            yield return "solve";
            yield return "strike";
            buttons[1].OnInteract();
            yield break;
        }
        if (command.EqualsIgnoreCase("clear"))
        {
            if (subButton || loading)
            {
                yield return "sendtochaterror You can't clear text right now!";
                yield break;
            }
            yield return null;
            while (displays[selectedDisp + 1].text != "")
            {
                displays[selectedDisp + 1].text = displays[selectedDisp + 1].text.Remove(displays[selectedDisp + 1].text.Length - 1, 1);
                yield return new WaitForSeconds(.05f);
            }
            yield break;
        }
        string[] parameters = command.Split(' ');
        if (parameters[0].EqualsIgnoreCase("type"))
        {
            if (parameters.Length == 1)
                yield return "sendtochaterror Please specify some text to type!";
            else
            {
                string input = command.Substring(5);
                for (int i = 0; i < input.Length; i++)
                {
                    if (!typableCharacters.Contains(input[i]) && !typableCharactersShift.Contains(input[i]))
                    {
                        yield return "sendtochaterror The character " + input[i] + " cannot be typed!";
                        yield break;
                    }
                }
                if (subButton || loading)
                {
                    yield return "sendtochaterror You can't type text right now!";
                    yield break;
                }
                yield return null;
                for (int i = 0; i < input.Length; i++)
                {
                    displays[selectedDisp + 1].text += input[i];
                    yield return new WaitForSeconds(.05f);
                }
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (loading) yield return true;
        if (failedToGen)
        {
            buttons[1].OnInteract();
            yield break;
        }
        if (subButton)
        {
            List<string> smashes1 = GetSmashedAnswers(displays[1].text, displays[2].text);
            List<string> smashes2 = GetSmashedAnswers(displays[2].text, displays[1].text);
            if (!(smashes1.Contains(generatedSmash) || smashes2.Contains(generatedSmash)))
            {
                StopAllCoroutines();
                moduleSolved = true;
                GetComponent<KMBombModule>().HandlePass();
            }
            else
                while (!moduleSolved) yield return true;
        }
        else
        {
            while (!exampleSol1.StartsWith(displays[selectedDisp + 1].text))
            {
                displays[selectedDisp + 1].text = displays[selectedDisp + 1].text.Remove(displays[selectedDisp + 1].text.Length - 1, 1);
                yield return new WaitForSeconds(.05f);
            }
            for (int i = displays[selectedDisp + 1].text.Length; i < exampleSol1.Length; i++)
            {
                displays[selectedDisp + 1].text += exampleSol1[i];
                yield return new WaitForSeconds(.05f);
            }
            int otherDisp = selectedDisp == 0 ? 1 : 0;
            if (exampleSol2 != displays[otherDisp + 1].text)
            {
                buttons[0].OnInteract();
                yield return new WaitForSeconds(.05f);
                while (!exampleSol2.StartsWith(displays[selectedDisp + 1].text))
                {
                    displays[selectedDisp + 1].text = displays[selectedDisp + 1].text.Remove(displays[selectedDisp + 1].text.Length - 1, 1);
                    yield return new WaitForSeconds(.05f);
                }
                for (int i = displays[selectedDisp + 1].text.Length; i < exampleSol2.Length; i++)
                {
                    displays[selectedDisp + 1].text += exampleSol2[i];
                    yield return new WaitForSeconds(.05f);
                }
            }
            buttons[1].OnInteract();
            while (!moduleSolved) yield return true;
        }
    }
}