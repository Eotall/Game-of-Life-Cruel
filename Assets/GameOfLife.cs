﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Newtonsoft.Json;

public class GameOfLife : MonoBehaviour {

	public KMBombInfo Info;
	public KMBombModule Module;
	public KMAudio Audio;

	public KMSelectable[] Btn;
	public KMSelectable Submit;
	public KMSelectable Reset;
	public MeshRenderer[] BtnColor;
	public TextMesh DisplayText;
	public Color32[] Colors;

	private int[] BtnColor1init = new int[48];
	private int[] BtnColor2init = new int[48];
	private int[] BtnColor1 = new int[48];
	private int[] BtnColor2 = new int[48];
	private int[] nCount = new int[48];
	private int Gen;
	private Color32[] ColorsSubmitted = new Color32[48];
	private Color32[] BtnColorStore = new Color32[48];
	private bool[] Rules = new bool[9]; 

	private int BlackAmount = 32;		// amount of black squares generated, at average, in initial setup
	private int WhiteAmount = 12;		// amount of white squares generated, at average, in initial setup
	private float TimeFlash = 0.5f;		// time between flashes
	private float TimeSuspend = 0.8f;	// time between generation when submitting
	private float TimeSneak = 0.4f;		// time the correct solution is displayed at a strike
	private float TimeTiny = 0.01f;		// time to allow computations in correct order. set to as low as possible
	private int GenRange = 3;			// maximum number of generations

	private int iiLast;
	private int iiBatteries;
	private int iiLit;
	private int iiUnlit;
	private int iiPortTypes;
	private int iiStrikes;
	private int iiSolved;
	private float iiTimeRemaining;
	private float iiTimeOriginal;
	private bool Bob;

	private bool isActive = false;
	private bool isSolved = false;
	private bool isSubmitting = false;

	private static int moduleIdCounter = 1;
	private int moduleId = 0;
	private string[] debugStore = new string[48];


	/////////////////////////////////////////////////// Initial Setup ///////////////////////////////////////////////////////

	// Loading screen
	void Start () {

		moduleId = moduleIdCounter++;
		Module.OnActivate += Activate;
	}

	// Lights off
	void Awake () {

		//run initial setup
		InitSetup ();

		//assign button presses
		Reset.OnInteract += delegate () {
			Audio.PlayGameSoundAtTransform (KMSoundOverride.SoundEffect.ButtonPress, Reset.transform);
			Reset.AddInteractionPunch ();
			if (isActive && !isSolved && !isSubmitting) {
				StartCoroutine (updateReset ());
			}
			return false;
		};

		Submit.OnInteract += delegate () {
			Audio.PlayGameSoundAtTransform (KMSoundOverride.SoundEffect.ButtonPress, Reset.transform);
			Reset.AddInteractionPunch ();
			if (isActive && !isSolved && !isSubmitting) {
				StartCoroutine (handleSubmit ());
			}
			return false;
		};
			
		for (int i = 0; i < 48; i++)
		{
			int j = i;
			Btn[i].OnInteract += delegate () {
				Audio.PlayGameSoundAtTransform (KMSoundOverride.SoundEffect.ButtonPress, Btn[j].transform);
				if (isActive && !isSolved && !isSubmitting) {
					handleSquare (j);
				}
				return false;
			};
		}
	}

	// Lights on
	void Activate () {

		updateBool ();

		StartCoroutine(updateTick ());

		StartCoroutine(updateDebug ());
	
		isActive = true;
	}

	// Initial setup
	void InitSetup () {

		Gen = Random.Range (2, (GenRange + 1));
		DisplayText.text = Gen.ToString ();

		iiTimeOriginal = Info.GetTime ();
		Bob = true;

		for (int i = 0; i < 48; i++)
		{
			// radomizing starting squares
			int x = Random.Range (0, 48);
			if (x < BlackAmount) {		// black, black
				BtnColor1init [i] = 0;
				BtnColor2init [i] = 0;
				BtnColor1 [i] = 0;
				BtnColor2 [i] = 0;
			} else {
				if (x < (BlackAmount + WhiteAmount)) {		// white, white
					BtnColor1init [i] = 1;
					BtnColor2init [i] = 1;
					BtnColor1 [i] = 1;
					BtnColor2 [i] = 1;
				} else {									// others randomized
					BtnColor1init [i] = Random.Range (0, 9);
					if (BtnColor1init [i] == 1)
						BtnColor1init [i] = 0;
					BtnColor2init [i] = Random.Range (0, 9);
					if (BtnColor2init [i] == 1)
						BtnColor2init [i] = 0;
					BtnColor1 [i] = BtnColor1init [i];
					BtnColor2 [i] = BtnColor2init [i];
				}
			}
		}
	}


	/////////////////////////////////////////////////// Updates ///////////////////////////////////////////////////////

	// update the booleans for rules
	void updateBool () {

		iiLast = Info.GetSerialNumberNumbers ().Last ();
		iiBatteries = Info.GetBatteryCount ();
		iiLit = Info.GetOnIndicators ().Count ();
		iiUnlit = Info.GetOffIndicators ().Count ();
		iiPortTypes = Info.GetPorts ().Distinct ().Count ();
		iiStrikes = Info.GetStrikes ();
		iiSolved = Info.GetSolvedModuleNames ().Count ();
		iiTimeRemaining = Info.GetTime ();

		if (iiStrikes > 0 && iiBatteries != 0) {																								//red		needs update
			Rules [2] = true;
		} else {
			Rules [2] = false;
		}
		if ((iiTimeRemaining < (iiTimeOriginal / 2)) && !Info.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.CAR)) {				//orange	needs update
			Rules [3] = true;
		} else {
			Rules [3] = false;
		}
		if ((iiLit > iiUnlit) && !Info.IsPortPresent (KMBombInfoExtensions.KnownPortType.RJ45)) {												//yellow
			Rules [4] = true;
		} else {
			Rules [4] = false;
		}
		if ((iiSolved %2 == 0) && !Info.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.CLR)) {										//green		needs update
			Rules [5] = true;
		} else {
			Rules [5] = false;
		}
		if (Info.GetSerialNumberLetters ().Any("seaky".Contains) && !Info.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.SND)) {	//blue
			Rules [6] = true;
		} else {
			Rules [6] = false;
		}
		if ((iiLit < iiUnlit) && (iiBatteries < 4)) {																							//purple
			Rules [7] = true;
		} else {
			Rules [7] = false;
		}
		if ((iiPortTypes > 2) && ((iiLit + iiUnlit) > 0)) {																						//brown
			Rules [8] = true;
		} else {
			Rules [8] = false;
		}
	}

	// automatic update of squares
	private IEnumerator updateTick () {

		if (!isActive || isSubmitting) {						// check if module is inactive or submitting. if yes, then wait.
			yield return new WaitForSeconds (TimeFlash);
			StartCoroutine(updateTick ());
		} else {
			StartCoroutine(updateSquares());
			yield return new WaitForSeconds (TimeFlash);
			StartCoroutine(updateTick ());
		}
	}

	// update the squares to correct colors
	private IEnumerator updateSquares () {

		for (int i = 0; i < 48; i++) {
			int j = i;
			if (BtnColor1 [i] == 0 && BtnColor2 [i] == 0) {					// if both are black
				BtnColor [j].material.color = Colors [BtnColor1 [j]];
			} else {
				if (BtnColor1 [i] == 1 && BtnColor2 [i] == 1) {					// if both are white
					BtnColor [j].material.color = Colors [BtnColor1 [j]];
				} else {															// all other cases
					if (BtnColor [i].material.color == Colors [BtnColor1 [i]]) {
						BtnColor [j].material.color = Colors [BtnColor2 [j]];
					} else {
						BtnColor [j].material.color = Colors [BtnColor1 [j]];
					}
				}
			}
		}
		yield return false;
	}

	// perform a reset to initial state
	private IEnumerator updateReset () {
		
		Debug.Log ("[Game of Life #" + moduleId + "] Module has been reset");
		for (int r = 0; r < 48; r++) {
			BtnColor1 [r] = BtnColor1init [r];
			BtnColor2 [r] = BtnColor2init [r];
		}
		StartCoroutine(updateSquares ());
		Bob = true;
		yield return new WaitForSeconds (TimeTiny);
		StartCoroutine(updateDebug ());
		yield return false;
	}

	// display current state in debug log
	private IEnumerator updateDebug () {

		yield return new WaitForSeconds (TimeTiny);

		for (int d = 0; d < 48; d++) {
			int e = d;
			if (BtnColor1 [d] == 0 && BtnColor2 [d] == 0) {
				debugStore [e] = "0";
			} else {
				if (BtnColor1 [d] == 1 && BtnColor2 [d] == 1) {
					debugStore [e] = "1";
				} else {
					debugStore [e] = "X";
				}
			}
		}

		Debug.Log ("[Game of Life #" + moduleId + "] The current state is (0 is black, 1 is white and X is colored): \n" + 
			debugStore[0] + " " + debugStore[1] + " " + debugStore[2] + " " + debugStore[3] + " " + debugStore[4] + " " + debugStore[5] + "\n" + 
			debugStore[6] + " " + debugStore[7] + " " + debugStore[8] + " " + debugStore[9] + " " + debugStore[10] + " " + debugStore[11] + "\n" + 
			debugStore[12] + " " + debugStore[13] + " " + debugStore[14] + " " + debugStore[15] + " " + debugStore[16] + " " + debugStore[17] + "\n" + 
			debugStore[18] + " " + debugStore[19] + " " + debugStore[20] + " " + debugStore[21] + " " + debugStore[22] + " " + debugStore[23] + "\n" + 
			debugStore[24] + " " + debugStore[25] + " " + debugStore[26] + " " + debugStore[27] + " " + debugStore[28] + " " + debugStore[29] + "\n" + 
			debugStore[30] + " " + debugStore[31] + " " + debugStore[32] + " " + debugStore[33] + " " + debugStore[34] + " " + debugStore[35] + "\n" + 
			debugStore[36] + " " + debugStore[37] + " " + debugStore[38] + " " + debugStore[39] + " " + debugStore[40] + " " + debugStore[41] + "\n" + 
			debugStore[42] + " " + debugStore[43] + " " + debugStore[44] + " " + debugStore[45] + " " + debugStore[46] + " " + debugStore[47]);
	}


	/////////////////////////////////////////////////// Button presses ///////////////////////////////////////////////////////

	// square is pressed
	void handleSquare (int num) {
		
		Bob = false;
		if (BtnColor [num].material.color == Colors [0]) {
			BtnColor [num].material.color = Colors [1];
			BtnColor1 [num] = 1;
			BtnColor2 [num] = 1;
		} else {
			BtnColor [num].material.color = Colors [0];
			BtnColor1 [num] = 0;
			BtnColor2 [num] = 0;
		}
	}

	// submit is pressed
	private IEnumerator handleSubmit () {

		Debug.Log ("[Game of Life #" + moduleId + "] Submit has been pressed. Calculating...");
		isSubmitting = true;

		// bob helps out? 
		if ((Info.GetBatteryCount () == 6) && (Info.GetBatteryHolderCount () == 3) && (Bob == true) && ((Info.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.BOB)) && (!Info.IsIndicatorOn(KMBombInfoExtensions.KnownIndicatorLabel.BOB)))) {
			Module.HandlePass ();
			Debug.Log ("[Game of Life #" + moduleId + "] Bob has assisted you. Time to party!");
			for (int i = 0; i < 48; i++) {
				BtnColor1[i] = Random.Range (2, 9);
				BtnColor2[i] = Random.Range (2, 9);
			}
			isSolved = true;
			isSubmitting = false;
		} else {

			// store the submitted color values
			for (int i = 0; i < 48; i++) {
				ColorsSubmitted [i] = BtnColor [i].material.color;
			}

			// run a reset
			StartCoroutine (updateReset ());
			yield return new WaitForSeconds (TimeTiny);

			// transform colored squares into black or white, according to rules (update rules first)
			updateBool ();
			yield return new WaitForSeconds (TimeTiny);

			for (int i = 0; i < 48; i++) {
				int j = i;
				if ((BtnColor1 [i] == 0 && BtnColor2 [i] == 0) || (BtnColor1 [i] == 1 && BtnColor2 [i] == 1)) {		// black or white, do nothing

				} else {



					if ((BtnColor1 [i] == 0 && BtnColor2 [i] == 2) || (BtnColor1 [i] == 2 && BtnColor2 [i] == 0)) {		// black+red
						if (Rules [2] == false) {
							BtnColor1 [j] = 1;
							BtnColor2 [j] = 1;
							Debug.Log ("[Game of Life #" + moduleId + "] Flashing red = White");
						} else {
							BtnColor1 [j] = 0;
							BtnColor2 [j] = 0;
							Debug.Log ("[Game of Life #" + moduleId + "] Flashing red = Black");
						}
					} else {
						if ((BtnColor1 [i] == 0 && BtnColor2 [i] == 3) || (BtnColor1 [i] == 3 && BtnColor2 [i] == 0)) {		// black+orange
							if (Rules [3] == false) {
								BtnColor1 [j] = 1;
								BtnColor2 [j] = 1;
								Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange = White");
							} else {
								BtnColor1 [j] = 0;
								BtnColor2 [j] = 0;
								Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange = Black");
							}
						} else {
							if ((BtnColor1 [i] == 0 && BtnColor2 [i] == 4) || (BtnColor1 [i] == 4 && BtnColor2 [i] == 0)) {		// black+yellow
								if (Rules [4] == false) {
									BtnColor1 [j] = 1;
									BtnColor2 [j] = 1;
									Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow = White");
								} else {
									BtnColor1 [j] = 0;
									BtnColor2 [j] = 0;
									Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow = Black");
								}
							} else {
								if ((BtnColor1 [i] == 0 && BtnColor2 [i] == 5) || (BtnColor1 [i] == 5 && BtnColor2 [i] == 0)) {		// black+green
									if (Rules [5] == false) {
										BtnColor1 [j] = 1;
										BtnColor2 [j] = 1;
										Debug.Log ("[Game of Life #" + moduleId + "] Flashing green = White");
									} else {
										BtnColor1 [j] = 0;
										BtnColor2 [j] = 0;
										Debug.Log ("[Game of Life #" + moduleId + "] Flashing green = Black");
									}
								} else {
									if ((BtnColor1 [i] == 0 && BtnColor2 [i] == 6) || (BtnColor1 [i] == 6 && BtnColor2 [i] == 0)) {		// black+blue
										if (Rules [6] == false) {
											BtnColor1 [j] = 1;
											BtnColor2 [j] = 1;
											Debug.Log ("[Game of Life #" + moduleId + "] Flashing blue = White");
										} else {
											BtnColor1 [j] = 0;
											BtnColor2 [j] = 0;
											Debug.Log ("[Game of Life #" + moduleId + "] Flashing blue = Black");
										}
									} else {
										if ((BtnColor1 [i] == 0 && BtnColor2 [i] == 7) || (BtnColor1 [i] == 7 && BtnColor2 [i] == 0)) {		// black+purple
											if (Rules [7] == false) {
												BtnColor1 [j] = 1;
												BtnColor2 [j] = 1;
												Debug.Log ("[Game of Life #" + moduleId + "] Flashing purple = White");
											} else {
												BtnColor1 [j] = 0;
												BtnColor2 [j] = 0;
												Debug.Log ("[Game of Life #" + moduleId + "] Flashing purple = Black");
											}
										} else {
											if ((BtnColor1 [i] == 0 && BtnColor2 [i] == 8) || (BtnColor1 [i] == 8 && BtnColor2 [i] == 0)) {		// black+brown
												if (Rules [8] == false) {
													BtnColor1 [j] = 1;
													BtnColor2 [j] = 1;
													Debug.Log ("[Game of Life #" + moduleId + "] Flashing brown = White");
												} else {
													BtnColor1 [j] = 0;
													BtnColor2 [j] = 0;
													Debug.Log ("[Game of Life #" + moduleId + "] Flashing brown = Black");
												}
											} else {



												if ((BtnColor1 [i] == 2 && BtnColor2 [i] == 2) || (BtnColor1 [i] == 2 && BtnColor2 [i] == 2)) {		// red
													if (Rules [2] == true) {
														BtnColor1 [j] = 1;
														BtnColor2 [j] = 1;
														Debug.Log ("[Game of Life #" + moduleId + "] Steady red = White");
													} else {
														BtnColor1 [j] = 0;
														BtnColor2 [j] = 0;
														Debug.Log ("[Game of Life #" + moduleId + "] Steady red = Black");
													}
												} else {
													if ((BtnColor1 [i] == 2 && BtnColor2 [i] == 3) || (BtnColor1 [i] == 3 && BtnColor2 [i] == 2)) {		// red+orange
														if (Rules [2] == true) {
															BtnColor1 [j] = 1;
															BtnColor2 [j] = 1;
															Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & orange = White");
														} else {
															BtnColor1 [j] = 0;
															BtnColor2 [j] = 0;
															Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & orange = Black");
														}
													} else {
														if ((BtnColor1 [i] == 2 && BtnColor2 [i] == 4) || (BtnColor1 [i] == 4 && BtnColor2 [i] == 2)) {		// red+yellow
															if (Rules [3] == true) {
																BtnColor1 [j] = 1;
																BtnColor2 [j] = 1;
																Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & yellow = White");
															} else {
																BtnColor1 [j] = 0;
																BtnColor2 [j] = 0;
																Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & yellow = Black");
															}
														} else {
															if ((BtnColor1 [i] == 2 && BtnColor2 [i] == 5) || (BtnColor1 [i] == 5 && BtnColor2 [i] == 2)) {		// red+green
																if (Rules [5] == true) {
																	BtnColor1 [j] = 1;
																	BtnColor2 [j] = 1;
																	Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & green = White");
																} else {
																	BtnColor1 [j] = 0;
																	BtnColor2 [j] = 0;
																	Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & green = Black");
																}
															} else {
																if ((BtnColor1 [i] == 2 && BtnColor2 [i] == 6) || (BtnColor1 [i] == 6 && BtnColor2 [i] == 2)) {		// red+blue
																	if (Rules [7] == true) {
																		BtnColor1 [j] = 1;
																		BtnColor2 [j] = 1;
																		Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & blue = White");
																	} else {
																		BtnColor1 [j] = 0;
																		BtnColor2 [j] = 0;
																		Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & blue = Black");
																	}
																} else {
																	if ((BtnColor1 [i] == 2 && BtnColor2 [i] == 7) || (BtnColor1 [i] == 7 && BtnColor2 [i] == 2)) {		// red+purple
																		if (Rules [2] == true) {
																			BtnColor1 [j] = 1;
																			BtnColor2 [j] = 1;
																			Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & purple = White");
																		} else {
																			BtnColor1 [j] = 0;
																			BtnColor2 [j] = 0;
																			Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & purple = Black");
																		}
																	} else {
																		if ((BtnColor1 [i] == 2 && BtnColor2 [i] == 8) || (BtnColor1 [i] == 8 && BtnColor2 [i] == 2)) {		// red+brown
																			if (iiLast % 2 == 0) {
																				if (Rules [8] == true) {
																					BtnColor1 [j] = 1;
																					BtnColor2 [j] = 1;
																					Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & brown (brown rule) = White");
																				} else {
																					BtnColor1 [j] = 0;
																					BtnColor2 [j] = 0;
																					Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & brown (brown rule) = Black");
																				}
																			} else {
																				if (Rules [2] == true) {
																					BtnColor1 [j] = 1;
																					BtnColor2 [j] = 1;
																					Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & brown (red rule) = White");
																				} else {
																					BtnColor1 [j] = 0;
																					BtnColor2 [j] = 0;
																					Debug.Log ("[Game of Life #" + moduleId + "] Flashing red & brown (red rule) = Black");
																				}
																			}
																		} else {



																			if ((BtnColor1 [i] == 3 && BtnColor2 [i] == 3) || (BtnColor1 [i] == 3 && BtnColor2 [i] == 3)) {		// orange
																				if (Rules [3] == true) {
																					BtnColor1 [j] = 1;
																					BtnColor2 [j] = 1;
																					Debug.Log ("[Game of Life #" + moduleId + "] Steady orange = White");
																				} else {
																					BtnColor1 [j] = 0;
																					BtnColor2 [j] = 0;
																					Debug.Log ("[Game of Life #" + moduleId + "] Steady orange = Black");
																				}
																			} else {
																				if ((BtnColor1 [i] == 3 && BtnColor2 [i] == 4) || (BtnColor1 [i] == 4 && BtnColor2 [i] == 3)) {		// orange+yellow
																					if (Rules [4] == true) {
																						BtnColor1 [j] = 1;
																						BtnColor2 [j] = 1;
																						Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & yellow = White");
																					} else {
																						BtnColor1 [j] = 0;
																						BtnColor2 [j] = 0;
																						Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & yellow = Black");
																					}
																				} else {
																					if ((BtnColor1 [i] == 3 && BtnColor2 [i] == 5) || (BtnColor1 [i] == 5 && BtnColor2 [i] == 3)) {		// orange+green
																						if (Rules [4] == true) {
																							BtnColor1 [j] = 1;
																							BtnColor2 [j] = 1;
																							Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & green = White");
																						} else {
																							BtnColor1 [j] = 0;
																							BtnColor2 [j] = 0;
																							Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & green = Black");
																						}
																					} else {
																						if ((BtnColor1 [i] == 3 && BtnColor2 [i] == 6) || (BtnColor1 [i] == 6 && BtnColor2 [i] == 3)) {		// orange+blue
																							if (Rules [3] == true) {
																								BtnColor1 [j] = 1;
																								BtnColor2 [j] = 1;
																								Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & blue = White");
																							} else {
																								BtnColor1 [j] = 0;
																								BtnColor2 [j] = 0;
																								Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & blue = Black");
																							}
																						} else {
																							if ((BtnColor1 [i] == 3 && BtnColor2 [i] == 7) || (BtnColor1 [i] == 7 && BtnColor2 [i] == 3)) {		// orange+purple
																								if (Rules [2] == true) {
																									BtnColor1 [j] = 1;
																									BtnColor2 [j] = 1;
																									Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & purple = White");
																								} else {
																									BtnColor1 [j] = 0;
																									BtnColor2 [j] = 0;
																									Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & purple = Black");
																								}
																							} else {
																								if ((BtnColor1 [i] == 3 && BtnColor2 [i] == 8) || (BtnColor1 [i] == 8 && BtnColor2 [i] == 3)) {		// orange+brown
																									if (iiLast % 2 == 0) {
																										if (Rules [8] == true) {
																											BtnColor1 [j] = 1;
																											BtnColor2 [j] = 1;
																											Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & brown (brown rule) = White");
																										} else {
																											BtnColor1 [j] = 0;
																											BtnColor2 [j] = 0;
																											Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & brown (brown rule) = Black");
																										}
																									} else {
																										if (Rules [3] == true) {
																											BtnColor1 [j] = 1;
																											BtnColor2 [j] = 1;
																											Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & brown (orange rule) = White");
																										} else {
																											BtnColor1 [j] = 0;
																											BtnColor2 [j] = 0;
																											Debug.Log ("[Game of Life #" + moduleId + "] Flashing orange & brown (orange rule) = Black");
																										}
																									}
																								} else {



																									if ((BtnColor1 [i] == 4 && BtnColor2 [i] == 4) || (BtnColor1 [i] == 4 && BtnColor2 [i] == 4)) {		// yellow
																										if (Rules [4] == true) {
																											BtnColor1 [j] = 1;
																											BtnColor2 [j] = 1;
																											Debug.Log ("[Game of Life #" + moduleId + "] Steady yellow = White");
																										} else {
																											BtnColor1 [j] = 0;
																											BtnColor2 [j] = 0;
																											Debug.Log ("[Game of Life #" + moduleId + "] Steady yellow = Black");
																										}
																									} else {
																										if ((BtnColor1 [i] == 4 && BtnColor2 [i] == 5) || (BtnColor1 [i] == 5 && BtnColor2 [i] == 4)) {		// yellow+green
																											if (Rules [4] == true) {
																												BtnColor1 [j] = 1;
																												BtnColor2 [j] = 1;
																												Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow & green = White");
																											} else {
																												BtnColor1 [j] = 0;
																												BtnColor2 [j] = 0;
																												Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow & green = Black");
																											}
																										} else {
																											if ((BtnColor1 [i] == 4 && BtnColor2 [i] == 6) || (BtnColor1 [i] == 6 && BtnColor2 [i] == 4)) {		// yellow+blue
																												if (Rules [5] == true) {
																													BtnColor1 [j] = 1;
																													BtnColor2 [j] = 1;
																													Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow & blue = White");
																												} else {
																													BtnColor1 [j] = 0;
																													BtnColor2 [j] = 0;
																													Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow & blue = Black");
																												}
																											} else {
																												if ((BtnColor1 [i] == 4 && BtnColor2 [i] == 7) || (BtnColor1 [i] == 7 && BtnColor2 [i] == 4)) {		// yellow+purple
																													if (Rules [7] == true) {
																														BtnColor1 [j] = 1;
																														BtnColor2 [j] = 1;
																														Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow & purple = White");
																													} else {
																														BtnColor1 [j] = 0;
																														BtnColor2 [j] = 0;
																														Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow & purple = Black");
																													}
																												} else {
																													if ((BtnColor1 [i] == 4 && BtnColor2 [i] == 8) || (BtnColor1 [i] == 8 && BtnColor2 [i] == 4)) {		// yellow+brown
																														if (iiLast % 2 == 0) {
																															if (Rules [8] == true) {
																																BtnColor1 [j] = 1;
																																BtnColor2 [j] = 1;
																																Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow & brown (brown rule) = White");
																															} else {
																																BtnColor1 [j] = 0;
																																BtnColor2 [j] = 0;
																																Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow & brown (brown rule) = Black");
																															}
																														} else {
																															if (Rules [4] == true) {
																																BtnColor1 [j] = 1;
																																BtnColor2 [j] = 1;
																																Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow & brown (yellow rule) = White");
																															} else {
																																BtnColor1 [j] = 0;
																																BtnColor2 [j] = 0;
																																Debug.Log ("[Game of Life #" + moduleId + "] Flashing yellow & brown (yellow rule) = Black");
																															}
																														}
																													} else {



																														if ((BtnColor1 [i] == 5 && BtnColor2 [i] == 5) || (BtnColor1 [i] == 5 && BtnColor2 [i] == 5)) {		// green
																															if (Rules [5] == true) {
																																BtnColor1 [j] = 1;
																																BtnColor2 [j] = 1;
																																Debug.Log ("[Game of Life #" + moduleId + "] Steady green = White");
																															} else {
																																BtnColor1 [j] = 0;
																																BtnColor2 [j] = 0;
																																Debug.Log ("[Game of Life #" + moduleId + "] Steady green = Black");
																															}
																														} else {
																															if ((BtnColor1 [i] == 5 && BtnColor2 [i] == 6) || (BtnColor1 [i] == 6 && BtnColor2 [i] == 5)) {		// green+blue
																																if (Rules [6] == true) {
																																	BtnColor1 [j] = 1;
																																	BtnColor2 [j] = 1;
																																	Debug.Log ("[Game of Life #" + moduleId + "] Flashing green & blue = White");
																																} else {
																																	BtnColor1 [j] = 0;
																																	BtnColor2 [j] = 0;
																																	Debug.Log ("[Game of Life #" + moduleId + "] Flashing green & blue = Black");
																																}
																															} else {
																																if ((BtnColor1 [i] == 5 && BtnColor2 [i] == 7) || (BtnColor1 [i] == 7 && BtnColor2 [i] == 5)) {		// green+purple
																																	if (Rules [6] == true) {
																																		BtnColor1 [j] = 1;
																																		BtnColor2 [j] = 1;
																																		Debug.Log ("[Game of Life #" + moduleId + "] Flashing green & purple = White");
																																	} else {
																																		BtnColor1 [j] = 0;
																																		BtnColor2 [j] = 0;
																																		Debug.Log ("[Game of Life #" + moduleId + "] Flashing green & purple = Black");
																																	}
																																} else {
																																	if ((BtnColor1 [i] == 5 && BtnColor2 [i] == 8) || (BtnColor1 [i] == 8 && BtnColor2 [i] == 5)) {		// green+brown
																																		if (iiLast % 2 == 0) {
																																			if (Rules [8] == true) {
																																				BtnColor1 [j] = 1;
																																				BtnColor2 [j] = 1;
																																				Debug.Log ("[Game of Life #" + moduleId + "] Flashing green & brown (brown rule) = White");
																																			} else {
																																				BtnColor1 [j] = 0;
																																				BtnColor2 [j] = 0;
																																				Debug.Log ("[Game of Life #" + moduleId + "] Flashing green & brown (brown rule) = Black");
																																			}
																																		} else {
																																			if (Rules [5] == true) {
																																				BtnColor1 [j] = 1;
																																				BtnColor2 [j] = 1;
																																				Debug.Log ("[Game of Life #" + moduleId + "] Flashing green & brown (green rule) = White");
																																			} else {
																																				BtnColor1 [j] = 0;
																																				BtnColor2 [j] = 0;
																																				Debug.Log ("[Game of Life #" + moduleId + "] Flashing green & brown (green rule) = Black");
																																			}
																																		}
																																	} else {



																																		if ((BtnColor1 [i] == 6 && BtnColor2 [i] == 6) || (BtnColor1 [i] == 6 && BtnColor2 [i] == 6)) {		// blue
																																			if (Rules [6] == true) {
																																				BtnColor1 [j] = 1;
																																				BtnColor2 [j] = 1;
																																				Debug.Log ("[Game of Life #" + moduleId + "] Steady blue = White");
																																			} else {
																																				BtnColor1 [j] = 0;
																																				BtnColor2 [j] = 0;
																																				Debug.Log ("[Game of Life #" + moduleId + "] Steady blue = Black");
																																			}
																																		} else {
																																			if ((BtnColor1 [i] == 6 && BtnColor2 [i] == 7) || (BtnColor1 [i] == 7 && BtnColor2 [i] == 6)) {		// blue+purple
																																				if (Rules [6] == true) {
																																					BtnColor1 [j] = 1;
																																					BtnColor2 [j] = 1;
																																					Debug.Log ("[Game of Life #" + moduleId + "] Flashing blue & purple = White");
																																				} else {
																																					BtnColor1 [j] = 0;
																																					BtnColor2 [j] = 0;
																																					Debug.Log ("[Game of Life #" + moduleId + "] Flashing blue & purple = Black");
																																				}
																																			} else {
																																				if ((BtnColor1 [i] == 6 && BtnColor2 [i] == 8) || (BtnColor1 [i] == 8 && BtnColor2 [i] == 6)) {		// blue+brown
																																					if (iiLast % 2 == 0) {
																																						if (Rules [8] == true) {
																																							BtnColor1 [j] = 1;
																																							BtnColor2 [j] = 1;
																																							Debug.Log ("[Game of Life #" + moduleId + "] Flashing blue & brown (brown rule) = White");
																																						} else {
																																							BtnColor1 [j] = 0;
																																							BtnColor2 [j] = 0;
																																							Debug.Log ("[Game of Life #" + moduleId + "] Flashing blue & brown (brown rule) = Black");
																																						}
																																					} else {
																																						if (Rules [6] == true) {
																																							BtnColor1 [j] = 1;
																																							BtnColor2 [j] = 1;
																																							Debug.Log ("[Game of Life #" + moduleId + "] Flashing blue & brown (blue rule) = White");
																																						} else {
																																							BtnColor1 [j] = 0;
																																							BtnColor2 [j] = 0;
																																							Debug.Log ("[Game of Life #" + moduleId + "] Flashing blue & brown (blue rule) = Black");
																																						}
																																					}
																																				} else {



																																					if ((BtnColor1 [i] == 7 && BtnColor2 [i] == 7) || (BtnColor1 [i] == 7 && BtnColor2 [i] == 7)) {		// purple
																																						if (Rules [7] == true) {
																																							BtnColor1 [j] = 1;
																																							BtnColor2 [j] = 1;
																																							Debug.Log ("[Game of Life #" + moduleId + "] Steady purple = White");
																																						} else {
																																							BtnColor1 [j] = 0;
																																							BtnColor2 [j] = 0;
																																							Debug.Log ("[Game of Life #" + moduleId + "] Steady purple = Black");
																																						}
																																					} else {
																																						if ((BtnColor1 [i] == 7 && BtnColor2 [i] == 8) || (BtnColor1 [i] == 8 && BtnColor2 [i] == 7)) {		// purple+brown
																																							if (iiLast % 2 == 0) {
																																								if (Rules [8] == true) {
																																									BtnColor1 [j] = 1;
																																									BtnColor2 [j] = 1;
																																									Debug.Log ("[Game of Life #" + moduleId + "] Flashing purple & brown (brown rule) = White");
																																								} else {
																																									BtnColor1 [j] = 0;
																																									BtnColor2 [j] = 0;
																																									Debug.Log ("[Game of Life #" + moduleId + "] Flashing purple & brown (brown rule) = Black");
																																								}
																																							} else {
																																								if (Rules [7] == true) {
																																									BtnColor1 [j] = 1;
																																									BtnColor2 [j] = 1;
																																									Debug.Log ("[Game of Life #" + moduleId + "] Flashing purple & brown (purple rule) = White");
																																								} else {
																																									BtnColor1 [j] = 0;
																																									BtnColor2 [j] = 0;
																																									Debug.Log ("[Game of Life #" + moduleId + "] Flashing purple & brown (purple rule) = Black");
																																								}
																																							}
																																						} else {



																																							if ((BtnColor1 [i] == 8 && BtnColor2 [i] == 8) || (BtnColor1 [i] == 8 && BtnColor2 [i] == 8)) {		// brown
																																								if (Rules [8] == true) {
																																									BtnColor1 [j] = 1;
																																									BtnColor2 [j] = 1;
																																									Debug.Log ("[Game of Life #" + moduleId + "] Steady brown = White");
																																								} else {
																																									BtnColor1 [j] = 0;
																																									BtnColor2 [j] = 0;
																																									Debug.Log ("[Game of Life #" + moduleId + "] Steady brown = Black");
																																								}
																																							} 
																																						}
																																					}
																																				}
																																			}
																																		}
																																	}
																																}
																															}
																														}
																													}
																												}
																											}
																										}
																									}
																								}
																							}
																						}
																					}
																				}
																			}
																		}
																	}
																}
															}
														}
													}
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}

			// update squares to show state of colors fixed, then wait for sneak
			StartCoroutine (updateSquares ());
			StartCoroutine (updateDebug ());
			yield return new WaitForSeconds (TimeSneak);

			// process the generations
			for (int g = 0; g < Gen; g++) {

				// store square color value
				for (int s = 0; s < 48; s++) {
					BtnColorStore [s] = BtnColor [s].material.color;
				}

				// process neighbours for each square
				for (int k = 0; k < 48; k++) {
					int l = k;
					nCount [l] = 0;
					// top left
					if (k - 7 < 0) {
					} else {
						if (BtnColorStore [(k - 7)].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// top
					if (k - 6 < 0) {
					} else {
						if (BtnColorStore [(k - 6)].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// top right
					if (k - 5 < 0) {
					} else {
						if (BtnColorStore [(k - 5)].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// left
					if (k - 1 < 0) {
					} else {
						if (BtnColorStore [(k - 1)].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// right
					if (k + 1 > 47) {
					} else {
						if (BtnColorStore [(k + 1)].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// bottom left
					if (k + 5 > 47) {
					} else {
						if (BtnColorStore [(k + 5)].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// bottom
					if (k + 6 > 47) {
					} else {
						if (BtnColorStore [(k + 6)].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// bottom right
					if (k + 7 > 47) {
					} else {
						if (BtnColorStore [(k + 7)].Equals (Colors [1])) {
							nCount [l]++;
						}
					}

					// read nCount and decide life state
					if (BtnColor [k].material.color == Colors [1]) {	//if square is white
						if (nCount [k] < 2 || nCount [k] > 3) {
							BtnColor [l].material.color = Colors [0]; 
							BtnColor1 [l] = 0;
							BtnColor2 [l] = 0;
						}
					} else {											//if square is black
						if (nCount [k] == 3) {
							BtnColor [l].material.color = Colors [1];
							BtnColor1 [l] = 1;
							BtnColor2 [l] = 1;
						}
					}
				}

				// update squares, wait, then next generation
				StartCoroutine (updateSquares ());
				StartCoroutine (updateDebug ());

				if (g < (Gen - 1)) {
					yield return new WaitForSeconds (TimeSuspend);
				} else {
					yield return new WaitForSeconds (TimeTiny);
				}
			}

			// test last generation vs ColorsSubmitted
			for (int i = 0; i < 48; i++) {
				if (isSubmitting == true) {
					//is any square wrongly submitted, then strike
					if (BtnColor [i].material.color != ColorsSubmitted [i]) {
						Debug.Log ("[Game of Life #" + moduleId + "] Submit pressed. First error found at square number " + (i + 1) + " in reading order");
						Module.HandleStrike ();
						yield return new WaitForSeconds (TimeSneak);
						isSubmitting = false;
						StartCoroutine (updateReset ());
					}
				}
			}
			//solve!
			if (isSubmitting == true) {
				Debug.Log ("[Game of Life #" + moduleId + "] Submit pressed. No errors found! Module passed");
				Module.HandlePass ();
				isSolved = true;
			}

			yield return false;
		}
	}
}