using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class ForteroudoScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public KMSelectable CheckSel;
    public KMSelectable ResetSel;
    public KMSelectable[] CompartmentSels;
    public TextMesh[] CompartmentTexts;
    public GameObject[] BlockObjs;
    public TextMesh[] BlockTexts;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;
    private bool _canInteract = true;

    private int _chosenAlphabet;
    private string[] _compartmentWords = new string[6];
    private string[] _shuffledCompartmentWords = new string[6];
    private string[] _blockWords = new string[6];
    private string[] _orderedBlocks = new string[6];
    private List<string> _alphedBlocks = new List<string>();

    private void Start()
    {
        _moduleId = _moduleIdCounter++;

        for (int i = 0; i < CompartmentSels.Length; i++)
            CompartmentSels[i].OnInteract += CompartmentPress(i);
        CheckSel.OnInteract += CheckPress;
        ResetSel.OnInteract += ResetPress;

        tryAgain:
        _chosenAlphabet = Rnd.Range(0, Data._alphabetSets.Length);
        if (_chosenAlphabet == Data._alphabetSets.Length - 1)
            _chosenAlphabet = Rnd.Range(0, Data._alphabetSets.Length);

        var randomWords = Data._allWords.Shuffle().Take(11).ToArray();
        for (int i = 0; i < 6; i++)
            _compartmentWords[i] = randomWords[i];
        for (int i = 6; i < 11; i++)
            _blockWords[i - 6] = randomWords[i];

        _compartmentWords = Alphabetize(_compartmentWords, Data._alphabetSets[_chosenAlphabet]);
        if (!IsUniqueAlphabeticalOrder(_compartmentWords))
            goto tryAgain;
        _shuffledCompartmentWords = _compartmentWords.Select(i => i.ToArray().Shuffle().Join("")).ToArray();

        _alphedBlocks = Alphabetize(_blockWords, Data._alphabetSets[_chosenAlphabet]).ToList();

        var list = new List<string[]>();
        for (int i = 0; i < 6; i++)
        {
            var newWords = _alphedBlocks.ToList();
            newWords.Insert(i, _compartmentWords[i]);
            if (newWords.SequenceEqual(Alphabetize(newWords.ToArray(), Data._alphabetSets[_chosenAlphabet])))
                list.Add(newWords.ToArray());
        }
        if (list.Count != 1)
            goto tryAgain;
        _orderedBlocks = list.First().ToArray();

        _blockWords.Shuffle();
        Debug.LogFormat("[Forteroudo #{0}] The chosen alphabet is {1}. ({2})", _moduleId, Data._alphabetNames[_chosenAlphabet], Data._alphabetSets[_chosenAlphabet].ToUpperInvariant());
        Debug.LogFormat("[Forteroudo #{0}] The compartments’ words, alphabetized, are {1}.", _moduleId, _compartmentWords.Join(", "));
        Debug.LogFormat("[Forteroudo #{0}] The compartments’ words, shuffled, are {1}.", _moduleId, _shuffledCompartmentWords.Join(", "));
        Debug.LogFormat("[Forteroudo #{0}] The blocks’ words, alphabetized, are {1}.", _moduleId, Alphabetize(_blockWords.Where(i => i != null).ToArray(), Data._alphabetSets[_chosenAlphabet]).Join(", "));
        Debug.LogFormat("<Forteroudo #{0}> A valid alphabetized order of blocks, including a compartment, is {1}.", _moduleId, _orderedBlocks.Join(", "));

        StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
        yield return null;
        for (int i = 0; i < 6; i++)
        {
            CompartmentTexts[i].text = _shuffledCompartmentWords[i];
            if (_blockWords[i] != null)
                BlockTexts[i].text = _blockWords[i];
            else
            {
                BlockObjs[i].SetActive(false);
                CompartmentSels[i].gameObject.SetActive(false);
            }
        }
    }

    private KMSelectable.OnInteractHandler CompartmentPress(int i)
    {
        return delegate ()
        {
            if (_blockWords[i] == null)
                return false;
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, CompartmentSels[i].transform);
            CompartmentSels[i].AddInteractionPunch(0.5f);
            int ix = Array.IndexOf(_blockWords, null);
            BlockObjs[ix].SetActive(true);
            CompartmentSels[ix].gameObject.SetActive(true);
            BlockTexts[ix].text = _blockWords[ix] = _blockWords[i];
            _blockWords[i] = null;
            BlockObjs[i].SetActive(false);
            CompartmentSels[i].gameObject.SetActive(false);
            return false;
        };
    }

    private bool CheckPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, CheckSel.transform);
        CheckSel.AddInteractionPunch(0.5f);
        if (!_canInteract)
            return false;

        Debug.LogFormat("[Forteroudo #{0}] Submitted the order: {1}.", _moduleId, _blockWords.Where(i => i != null).ToArray().Join(", "));
        var alphedWords = Alphabetize(_blockWords, Data._alphabetSets[_chosenAlphabet]);
        if (!_blockWords.Where(i => i != null).SequenceEqual(alphedWords))
        {
            Audio.PlaySoundAtTransform("strike", transform);
            Debug.LogFormat("[Forteroudo #{0}] This is not in the correct alphabetical order. Strike.", _moduleId);
            Module.HandleStrike();
            return false;
        }

        _canInteract = false;
        Debug.LogFormat("[Forteroudo #{0}] This is in the correcct alphabetical order. Module solved.", _moduleId);

        var newWords = Enumerable.Range(0, 6).Select(i => _blockWords[i] ?? _compartmentWords[i]).ToArray();
        var newAlphedWords = Alphabetize(newWords, Data._alphabetSets[_chosenAlphabet]);

        if (newWords.SequenceEqual(newAlphedWords))
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
            Debug.LogFormat("[Forteroudo #{0}] The compartment word, included with the block words, also is in alphabetical order!", _moduleId);
            _moduleSolved = true;
            Module.HandlePass();
            return false;
        }

        StartCoroutine(LongSolveLmao());
        return false;
    }

    private bool ResetPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ResetSel.transform);
        CheckSel.AddInteractionPunch(0.5f);
        if (!_canInteract)
            return false;

        tryAgain:
        var compWordsToPickFrom = Data._allWords.Where(i => !_blockWords.Contains(i)).ToArray();
        var pickedWords = compWordsToPickFrom.Shuffle().Take(6).ToArray();
        pickedWords = Alphabetize(pickedWords, Data._alphabetSets[_chosenAlphabet]);

        var list = new List<string[]>();
        for (int i = 0; i < 6; i++)
        {
            var newWords = _alphedBlocks.ToList();
            newWords.Insert(i, pickedWords[i]);
            if (newWords.SequenceEqual(Alphabetize(newWords.ToArray(), Data._alphabetSets[_chosenAlphabet])))
                list.Add(newWords.ToArray());
        }
        if (list.Count != 1)
            goto tryAgain;
        _orderedBlocks = list.First().ToArray();

        for (int i = 0; i < 6; i++)
            _compartmentWords[i] = pickedWords[i];
        _shuffledCompartmentWords = _compartmentWords.Select(i => i.ToArray().Shuffle().Join("")).ToArray();
        for (int i = 0; i < 6; i++)
            CompartmentTexts[i].text = _shuffledCompartmentWords[i];

        Debug.LogFormat("[Forteroudo #{0}] Reset pressed.", _moduleId);
        Debug.LogFormat("[Forteroudo #{0}] The compartments’ new words, alphabetized, are {1}.", _moduleId, _compartmentWords.Join(", "));
        Debug.LogFormat("[Forteroudo #{0}] The compartments’ new words, shuffled, are {1}.", _moduleId, _shuffledCompartmentWords.Join(", "));
        Debug.LogFormat("<Forteroudo #{0}> A valid alphabetized order of blocks, including a compartment, is {1}.", _moduleId, _orderedBlocks.Join(", "));
        return false;
    }

    private IEnumerator LongSolveLmao()
    {
        Audio.PlaySoundAtTransform("solve", transform);
        yield return new WaitForSeconds(22f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
        _moduleSolved = true;
        Module.HandlePass();
    }

    private string[] Alphabetize(string[] words, string alphabet)
    {
        return words.Where(i => i != null).OrderBy(x => x.Select(y => "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[alphabet.IndexOf(y)]).Join("")).ToArray();
    }

    private bool IsUniqueAlphabeticalOrder(string[] words)
    {
        int count = 0;
        for (int i = 0; i < Data._alphabetSets.Length; i++)
        {
            var alphed = Alphabetize(words, Data._alphabetSets[i]);
            if (words.SequenceEqual(alphed))
                count++;
        }
        return count == 1;
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} <word> <word> <word> [Press blocks with the written words.] | !{0} check [press the CHECK button.]";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();
        if (command == "check")
        {
            yield return null;
            yield return "solve";
            CheckSel.OnInteract();
            yield break;
        }
        var cmds = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (cmds.Length < 1)
            yield break;
        var list = new List<string>();
        for (int i = 0; i < cmds.Length; i++)
        {
            var word = cmds[i];
            if (!_blockWords.Contains(cmds[i]))
            {
                yield return "sendtochaterror " + word + " is not present on any of the blocks. Command ignored.";
                yield break;
            }
            list.Add(word);
        }
        yield return null;
        for (int i = 0; i < list.Count; i++)
        {
            CompartmentSels[Array.IndexOf(_blockWords, list[i])].OnInteract();
            yield return new WaitForSeconds(0.3f);
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        for (int w = 0; w < 6; w++)
        {
            if (w == Array.IndexOf(_orderedBlocks.Select(j => Array.IndexOf(_blockWords, j)).ToArray(), -1))
                continue;
            if (_blockWords[w] == _orderedBlocks[w])
                continue;
            if (_blockWords[w] != null)
            {
                CompartmentSels[w].OnInteract();
                yield return new WaitForSeconds(0.3f);
            }
            CompartmentSels[Array.IndexOf(_blockWords, _orderedBlocks[w])].OnInteract();
            yield return new WaitForSeconds(0.3f);
        }
        CheckSel.OnInteract();
        while (!_moduleSolved)
            yield return true;
    }
}