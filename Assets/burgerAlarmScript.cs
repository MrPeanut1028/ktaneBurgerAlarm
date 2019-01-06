using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using KModkit;

public class burgerAlarmScript : MonoBehaviour {

    public KMBombModule Module;
    public KMBombInfo Info;
    public KMAudio Audio;
    public KMSelectable[] btns;
    public KMSelectable order, submit;
    public TextMesh numberText, timerText;
    public Transform[] textureTransforms;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool solved;

    private int[] buttonSymbols = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }; // Which symbol is on [BUTTON #]?
    private int[] symbolPositions = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }; // Which button is [SYMBOL #] on?
    private static readonly string[] symbolNames = { "mayo", "bun", "tomato", "cheese", "lettuce", "onions", "pickles", "mustard", "ketchup", "meat" };
    //                                                 0       1      2         3         4          5         6          7          8          9
    private int[] number = { 0, 0, 0, 0, 0, 0, 0 };

    private int[,] table =
    {
        { 0, 6, 3, 4, 8, 5, 0, 6, 5, 5 },
        { 5, 1, 0, 6, 8, 1, 7, 7, 5, 6 },
        { 6, 2, 3, 2, 9, 4, 3, 8, 5, 1 },
        { 8, 8, 3, 8, 3, 9, 2, 2, 6, 7 },
        { 6, 9, 9, 1, 7, 9, 8, 2, 4, 1 },
        { 4, 9, 8, 2, 0, 8, 0, 5, 0, 9 },
        { 9, 1, 1, 1, 9, 6, 2, 7, 5, 3 },
        { 1, 7, 3, 6, 0, 0, 0, 0, 4, 2 },
        { 5, 4, 1, 9, 2, 7, 2, 3, 4, 8 },
        { 3, 8, 4, 7, 6, 3, 7, 4, 5, 4 }
    };

    private static readonly string[] primes = { "2", "3", "5", "7" };
    private bool currentlyOrdering = false, finishedIncreasing = false;

    private int[] rowOrders = { 0, 0, 0, 0, 0 };
    private int[] colOrders = { 0, 0, 0, 0, 0 };

    private string[] orderStrings = { "", "", "", "", "" };
    private int shownOrder = 0;
    private int[] btnsToPress = { 1, 0, 0, 0, 0, 0, 1 };
    private int btnsPressed = 0;
    private bool sequenceCorrect = true;
    private string[] reasonsForStrike = { "", "", "", "", "", "", "" };

    private int[] swaps = { 0, 0, 0, 0, 0, 0, 0, 0 };

    void Start () {
        _moduleId = _moduleIdCounter++;
        Module.OnActivate += SetUpButtons;
    }

    void SetUpButtons()
    {
        order.OnInteract += delegate ()
        {
            if (!solved)
                Order();
            order.AddInteractionPunch();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Module.transform);
            return false;
        };

        submit.OnInteract += delegate ()
        {
            if (!solved)
                Submit();
            submit.AddInteractionPunch(10);
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Module.transform);
            return false;
        };

        for (int i = 0; i < btns.Length; i++)
        {
            int j = i;

            btns[i].OnInteract += delegate ()
            {
                if (!solved && finishedIncreasing && currentlyOrdering)
                    BtnPress(j);
                btns[j].AddInteractionPunch();
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Module.transform);
                return false;
            };
        }
        
        GenerateModule();
    }

    void GenerateModule()
    {
        // Randomize buttons.
        buttonSymbols = buttonSymbols.Shuffle().ToArray();
        var originalTransforms = textureTransforms.Select(tx => tx.transform.localPosition).ToArray();

        for (int i = 0; i < 10; i++)
        {
            textureTransforms[buttonSymbols[i]].transform.localPosition = originalTransforms[i];
        }
        
        for (int i = 0; i < 10; i++)
        {
            DebugMsg("Button #" + (i + 1) + " has " + symbolNames[buttonSymbols[i]] + " on it.");

            symbolPositions[buttonSymbols[i]] = i;
        }

        // Randomize number.

        numberText.text = "";

        for (int i = 0; i < number.Length; i++)
        {
            number[i] = Random.Range(0, 10);
            numberText.text += number[i].ToString();
        }

        timerText.text = "";

        // Calculate answer.

        var tableOffsets = new int[8];

        // Number #1...
        
        if (primes.Contains(Info.GetSerialNumber()[5].ToString()))
        {
            if (buttonSymbols[3] == 8 || buttonSymbols[4] == 8 || buttonSymbols[5] == 8)
                tableOffsets[0] = 7;

            else
                tableOffsets[0] = 4;
        }

        else
        {
            if (buttonSymbols[2] == 3 || buttonSymbols[5] == 3 || buttonSymbols[8] == 3)
                tableOffsets[0] = 6;

            else
                tableOffsets[0] = 3;
        }

        // Number #2...

        if (Info.GetSolvableModuleNames().Contains("The Clock") || Info.GetSolvableModuleNames().Contains("Rubik's Clock"))
        {
            if (symbolPositions[6] / 3 == symbolPositions[0] / 3)
                tableOffsets[1] = 0;

            else
                tableOffsets[1] = 1;
        }

        else
        {
            if (symbolPositions[2] % 3 == symbolPositions[1] % 3)
                tableOffsets[1] = 8;

            else if ((symbolPositions[1] % 3 == 1 && symbolPositions[2] == 9) || symbolPositions[2] % 3 == 1 && symbolPositions[1] == 9)
                tableOffsets[1] = 8;

            else
                tableOffsets[1] = 2;
        }

        // Number #3...

        if (Info.GetBatteryCount(Battery.D) == 0)
        {
            if (symbolPositions[7] < 6 && symbolPositions[7] < 8)
                tableOffsets[2] = 5;
                             
            else             
                tableOffsets[2] = 9;
        }

        else
        {
            if (symbolPositions[9] % 3 != 1 || symbolPositions[9] == 9)
                tableOffsets[2] = 3;

            else
                tableOffsets[2] = 7;
        }

        // Number #4...

        if (Info.IsPortPresent(Port.HDMI) || Info.IsPortPresent(Port.PCMCIA))
        {
            if (symbolPositions[4] > 7)
                tableOffsets[3] = 1;

            else
                tableOffsets[3] = 0;
        }

        else
        {
            if (FindAdjacentBtns(6).Contains(symbolPositions[7]))
                tableOffsets[3] = 4;

            else
                tableOffsets[3] = 8;
        }

        // Number #5...

        if (Info.IsTwoFactorPresent())
        {
            if (symbolPositions[5] != 6 && symbolPositions[5] != 9)
                tableOffsets[4] = 8;

            else
                tableOffsets[4] = 3;
        }

        else
        {
            if (!FindAdjacentBtns(8).Contains(symbolPositions[0]))
                tableOffsets[4] = 6;

            else
                tableOffsets[4] = 9;
        }

        // Number #6...

        if (Info.IsIndicatorPresent(Indicator.NLL) || Info.IsIndicatorPresent(Indicator.SND))
        {
            if (symbolPositions[2] > 5)
                tableOffsets[5] = 1;

            else
                tableOffsets[5] = 0;
        }

        else
        {
            if (symbolPositions[1] < 6)
                tableOffsets[5] = 4;

            else
                tableOffsets[5] = 5;
        }

        // Number #7...

        if (Info.GetSerialNumber().Contains("B") || Info.GetSerialNumber().Contains("U") || Info.GetSerialNumber().Contains("R") || Info.GetSerialNumber().Contains("G") || Info.GetSerialNumber().Contains("3"))
        {
            if (symbolPositions[3] / 3 > symbolPositions[0] / 3)
                tableOffsets[6] = 5;

            else
                tableOffsets[6] = 9;
        }

        else
        {
            if (symbolPositions[8] / 3 < symbolPositions[1] / 3) 
                tableOffsets[6] = 3;

            else
                tableOffsets[6] = 7;
        }

        // Number #8...

        if (Info.GetSolvableModuleNames().Contains("Ice Cream") || Info.GetSolvableModuleNames().Contains("Cooking") || Info.GetSolvableModuleNames().Contains("Cookie Jars"))
        {
            if (symbolPositions[7] % 3 < symbolPositions[9] % 3)
                tableOffsets[7] = 1;

            else
                tableOffsets[7] = 0;
        }

        else
        {
            if (symbolPositions[1] % 3 > symbolPositions[4] % 3)
                tableOffsets[7] = 4;

            else
                tableOffsets[7] = 8;
        }

        DebugMsg("The number displayed is " + numberText.text);

        for (int i = 0; i < 8; i++)
        {
            DebugMsg("The answer from Table #" + (i + 1) + " was " + tableOffsets[i] + ".");
            
            if (i != 7)
            {
                swaps[i] = (tableOffsets[i] + number[i]) % 10;
            }

            else
            {
                swaps[i] = (tableOffsets[i] + number[0] + number[1] + number[2] + number[3] + number[4] + number[5] + number[6]) % 10;
            }
        }

        // Mess with table

        int swappedThing;

        for (int i = 0; i < 10; i++)
        {
            swappedThing = table[swaps[0], i];
            table[swaps[0], i] = table[swaps[1], i];
            table[swaps[1], i] = swappedThing;
        }

        DebugMsg("Swapping rows " + swaps[0] + " and " + swaps[1] + ".");

        for (int i = 0; i < 10; i++)
        {
            swappedThing = table[i, swaps[2]];
            table[i, swaps[2]] = table[i, swaps[3]];
            table[i, swaps[3]] = swappedThing;
        }

        DebugMsg("Swapping columns " + swaps[2] + " and " + swaps[3] + ".");

        if ((swaps[0] != swaps[4] && swaps[0] != swaps[5] && swaps[1] != swaps[4] && swaps[1] != swaps[5]) || swaps[0] == swaps[1])
        {
            for (int i = 0; i < 10; i++)
            {
                swappedThing = table[swaps[4], i];
                table[swaps[4], i] = table[swaps[5], i];
                table[swaps[5], i] = swappedThing;
            }

            DebugMsg("Swapping rows " + swaps[4] + " and " + swaps[5] + ".");
        }

        if ((swaps[2] != swaps[6] && swaps[2] != swaps[7] && swaps[3] != swaps[6] && swaps[3] != swaps[7]) || swaps[2] == swaps[3])
        {
            for (int i = 0; i < 10; i++)
            {
                swappedThing = table[i, swaps[6]];
                table[i, swaps[6]] = table[i, swaps[7]];
                table[i, swaps[7]] = swappedThing;
            }
            
            DebugMsg("Swapping columns " + swaps[6] + " and " + swaps[7] + ".");
        }

        for (int i = 0; i < 10; i++)
        {
            DebugMsg("Row " + i + " is " + symbolNames[table[i, 0]] + ", " + symbolNames[table[i, 1]] + ", " + symbolNames[table[i, 2]] + ", " + symbolNames[table[i, 3]] + ", "
                + symbolNames[table[i, 4]] + ", " + symbolNames[table[i, 5]] + ", " + symbolNames[table[i, 6]] + ", "
                + symbolNames[table[i, 7]] + ", " + symbolNames[table[i, 8]] + ", and " + symbolNames[table[i, 9]] + ".");
        }
    }

    void Order()
    {
        if (!currentlyOrdering)
        {
            currentlyOrdering = true;
            finishedIncreasing = false;

            DebugMsg("You pressed order!");

            StartCoroutine(Timer());

            // Generate order
            
            for (int i = 0; i < 5; i++)
            {
                rowOrders[i] = Random.Range(0, 10);
                colOrders[i] = Random.Range(0, 10);

                btnsToPress[i + 1] = table[rowOrders[i], colOrders[i]];
                orderStrings[i] = "no.    " + rowOrders[i] + colOrders[i];

                DebugMsg("Order #" + (i + 1) + " is " + orderStrings[i].Replace("    ", " ") + ".");
                DebugMsg("That means you should press " + symbolNames[btnsToPress[i + 1]] + ".");
            }
            
            sequenceCorrect = true;
            
            for (int i = 0; i < 7; i++)
            {
                reasonsForStrike[i] = "";
            }
        }

        else if (finishedIncreasing)
        {
            StartCoroutine(ChangeOrder());
        }
    }

    void Submit()
    {
        DebugMsg("You pressed submit!");

        if (!currentlyOrdering)
        {
            Module.HandleStrike();

            DebugMsg("That ain't right, because...");

            DebugMsg("Nobody even ordered anything.");

            DebugMsg("STRIKE!!!");
            StartCoroutine(StrikeAnimation());

            if (Random.Range(0, 2) == 0)
            {
                Audio.PlaySoundAtTransform("NoThisIsPatrick", Module.transform);
            }

            else
            {
                Audio.PlaySoundAtTransform("NumberFifteen", Module.transform);
            }
        }

        else if (btnsPressed < 7)
        {
            Module.HandleStrike();

            DebugMsg("That ain't right, because...");

            for (int i = 0; i < 7; i++)
            {
                if (reasonsForStrike[i] != "")
                {
                    DebugMsg(reasonsForStrike[i]);
                }
            }

            DebugMsg("The burger's not big enough. The customer starves to death and you get fired.");

            DebugMsg("STRIKE!!!");
            StartCoroutine(StrikeAnimation());

            if (Random.Range(0, 2) == 0)
            {
                Audio.PlaySoundAtTransform("NoThisIsPatrick", Module.transform);
            }

            else
            {
                Audio.PlaySoundAtTransform("NumberFifteen", Module.transform);
            }
        }

        else if (sequenceCorrect && currentlyOrdering)
        {
            Module.HandlePass();
            solved = true;

            DebugMsg("Looks like that was right. Module solved!");

            numberText.text = "GG.";
        }

        else
        {
            Module.HandleStrike();

            DebugMsg("That ain't right, because...");

            for (int i = 0; i < 7; i++)
            {
                if (reasonsForStrike[i] != "")
                {
                    DebugMsg(reasonsForStrike[i]);
                }
            }

            DebugMsg("STRIKE!!!");
            StartCoroutine(StrikeAnimation());

            if (Random.Range(0, 2) == 0)
            {
                Audio.PlaySoundAtTransform("NoThisIsPatrick", Module.transform);
            }

            else
            {
                Audio.PlaySoundAtTransform("NumberFifteen", Module.transform);
            }
        }

        currentlyOrdering = false;
        btnsPressed = 0;
    }

    void BtnPress(int btnNum)
    {
        DebugMsg("You pressed the button with the " + symbolNames[buttonSymbols[btnNum]] + ".");

        if (btnsPressed >= 0 && btnsPressed <= 6)
        {
            if (btnsToPress[btnsPressed] != buttonSymbols[btnNum])
            {
                sequenceCorrect = false;

                if (btnsPressed == 0 || btnsPressed == 6)
                {
                    reasonsForStrike[0] = "These customers don't want none unless you got BUNS, hun!";
                }

                else
                {
                    reasonsForStrike[btnsPressed] = "Ingredient " + btnsPressed + " was wrong...";
                }
            }
        }

        else
        {
            sequenceCorrect = false;

            reasonsForStrike[6] = ("The burger's too big! Your customer has a heart attack and you're fired.");
        }

        btnsPressed++;
    }

    void DebugMsg(string msg)
    {
        Debug.LogFormat("[Burger Alarm #{0}] {1}", _moduleId, msg);
    }

    int[] FindAdjacentBtns(int symbolNum)
    {
        int[] adjacentBtns = { 10, 10, 10, 10 };

        if (symbolPositions[symbolNum] == 0)
        {
            adjacentBtns[0] = 1;
            adjacentBtns[1] = 3;
        }

        else if (symbolPositions[symbolNum] == 1)
        {
            adjacentBtns[0] = 0;
            adjacentBtns[1] = 2;
            adjacentBtns[2] = 4;
        }

        else if (symbolPositions[symbolNum] == 2)
        {
            adjacentBtns[0] = 1;
            adjacentBtns[1] = 5;
        }

        else if (symbolPositions[symbolNum] == 3)
        {
            adjacentBtns[0] = 0;
            adjacentBtns[1] = 4;
            adjacentBtns[2] = 6;
        }

        else if (symbolPositions[symbolNum] == 4)
        {
            adjacentBtns[0] = 1;
            adjacentBtns[1] = 3;
            adjacentBtns[2] = 5;
            adjacentBtns[3] = 7;
        }

        else if (symbolPositions[symbolNum] == 5)
        {
            adjacentBtns[0] = 2;
            adjacentBtns[1] = 4;
            adjacentBtns[2] = 8;
        }

        else if (symbolPositions[symbolNum] == 6)
        {
            adjacentBtns[0] = 3;
            adjacentBtns[1] = 7;
        }

        else if (symbolPositions[symbolNum] == 7)
        {
            adjacentBtns[0] = 4;
            adjacentBtns[1] = 6;
            adjacentBtns[2] = 8;
            adjacentBtns[3] = 9;
        }

        else if (symbolPositions[symbolNum] == 8)
        {
            adjacentBtns[0] = 7;
            adjacentBtns[1] = 5;
        }

        else
        {
            adjacentBtns[0] = 7;
        }

        return adjacentBtns;
    }

    IEnumerator Timer()
    {
        int time = 0;

        for (int i = 0; i < 91; i++)
        {
            if (time.ToString().Length == 1)
            {
                timerText.text = "0" + time.ToString();
            }

            else
            {
                timerText.text = time.ToString();
            }

            time++;
            numberText.text = "";

            for (int x = 0; x < 7; x++)
            {
                numberText.text += Random.Range(0, 10).ToString();
            }

            yield return new WaitForSeconds(.005f);
        }
        
        finishedIncreasing = true;

        numberText.text = "HURRYUP";

        yield return new WaitForSeconds(1f);

        numberText.text = orderStrings[0];

        yield return new WaitForSeconds(.5f);

        while (time != 0 && currentlyOrdering)
        {
            time--;
            timerText.text = time.ToString();

            yield return new WaitForSeconds(1);
        }

        timerText.text = "";

        if (currentlyOrdering)
        {
            Module.HandleStrike();
            currentlyOrdering = false;
            DebugMsg("Your customer got impatient and left. STRIKE!!!");
            StartCoroutine(StrikeAnimation());

            if (Random.Range(0, 2) == 0)
            {
                Audio.PlaySoundAtTransform("NoThisIsPatrick", Module.transform);
            }

            else
            {
                Audio.PlaySoundAtTransform("NumberFifteen", Module.transform);
            }
        }
    }

    IEnumerator ChangeOrder()
    {
        numberText.text = "";

        yield return new WaitForSeconds(.25f);
        shownOrder = (shownOrder + 1) % 5;

        numberText.text = orderStrings[shownOrder];
    }

    IEnumerator StrikeAnimation()
    {
        numberText.color = Color.red;

        for (int i = 0; i < 100; i++)
        {
            yield return new WaitForSeconds(.01f);
            
            numberText.text = "";

            for (int x = 0; x < 7; x++)
                numberText.text += Random.Range(0, 10).ToString();

            timerText.text = Random.Range(0, 10).ToString() + Random.Range(0, 10).ToString();
        }

        numberText.color = new Color32(50, 225, 50, 255);
        numberText.text = "";

        for (int i = 0; i < 7; i++)
        {
            numberText.text += number[i].ToString();
        }

        timerText.text = "";
    }
}
