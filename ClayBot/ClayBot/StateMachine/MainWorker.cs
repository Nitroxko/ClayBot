﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace ClayBot.StateMachine
{
    partial class MainWorker
    {
        private MainForm mainForm;
        private Dictionary<State, Transition> transitions;
        private bool mainLoop;
        private Transition currentTransition;
        
        public MainWorker(MainForm mainForm)
        {
            mainLoop = true;
            this.mainForm = mainForm;

            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(mainForm.Config.LolLocale);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(mainForm.Config.LolLocale);

            currentTransition = new Transition(
                State.Unknown,
                () => { return true; },
                () => { },
                (Enum.GetValues(typeof(State)) as State[]).Where(x => x != State.Unknown).ToArray());

            transitions = new Dictionary<State, Transition>()
            {
                #region Unknown State
                { State.Unknown, currentTransition },
                #endregion
                
                #region Initial State
                { State.Initial, new Transition(
                    State.Initial,
                    () =>
                    {
                        int processCount = 0;

                        foreach (string processName in Static.PROCESS_NAMES)
                        {
                            processCount += Process.GetProcessesByName(processName).Count();
                        }

                        return processCount == 0;
                    },
                    () =>
                    {
                        new Process()
                        {
                            StartInfo = new ProcessStartInfo()
                            {
                                FileName = mainForm.Config.LolLauncherPath
                            }
                        }.Start();
                    },
                    new State[]
                    {
                        State.Patcher,
                        State.Patching,
                        State.PatcherAgreement
                    }) },
                #endregion

                #region Patching State
                { State.Patching, new Transition(
                    State.Patching,
                    () =>
                    {
                        PatcherSize patcherSize;
                        if (!CheckPatcher(out patcherSize)) return false;

                        ActivateTargetWindow(Static.PATCHER_SIZES[patcherSize]);

                        return
                            ValidatePatcher(new PatcherValidation[]
                            {
                                new PatcherValidation()
                                {
                                    ExpectedResult = true,
                                    PatcherRectangle = PatcherRectangle.PatcherIndicator,
                                    PatcherSize = patcherSize,
                                    IsThreshold = false
                                },
                                new PatcherValidation()
                                {
                                    ExpectedResult = true,
                                    PatcherRectangle = PatcherRectangle.Online,
                                    PatcherSize = patcherSize,
                                    IsThreshold = false
                                },
                                new PatcherValidation()
                                {
                                    ExpectedResult = false,
                                    PatcherRectangle = PatcherRectangle.Launch,
                                    PatcherSize = patcherSize,
                                    IsThreshold = false
                                }
                            });
                    },
                    () =>
                    {
                    },
                    new State[]
                    {
                        State.Patching,
                        State.Patcher
                    }) },
                #endregion

                #region Patcher State
                { State.Patcher, new Transition(
                    State.Patcher,
                    () =>
                    {
                        PatcherSize patcherSize;
                        if (!CheckPatcher(out patcherSize)) return false;

                        ActivateTargetWindow(Static.PATCHER_SIZES[patcherSize]);

                        return ValidatePatcher(new PatcherValidation[]
                        {
                            new PatcherValidation()
                            {
                                ExpectedResult = true,
                                PatcherRectangle = PatcherRectangle.PatcherIndicator,
                                PatcherSize = patcherSize,
                                IsThreshold = false
                            },
                            new PatcherValidation()
                            {
                                ExpectedResult = true,
                                PatcherRectangle = PatcherRectangle.Online,
                                PatcherSize = patcherSize,
                                IsThreshold = false
                            },
                            new PatcherValidation()
                            {
                                ExpectedResult = true,
                                PatcherRectangle = PatcherRectangle.Launch,
                                PatcherSize = patcherSize,
                                IsThreshold = false
                            }
                        });
                    },
                    () =>
                    {
                        PatcherSize patcherSize;
                        CheckPatcher(out patcherSize);
                        ClickTargetWindowRectangle(Static.PATCHER_RECTANGLES[PatcherRectangle.Launch][patcherSize]);
                    },
                    new State[]
                    {
                        State.PatcherAgreement,
                        State.Login
                    }) },
                #endregion

                #region Patcher Agreement State
                { State.PatcherAgreement, new Transition(
                    State.PatcherAgreement,
                    () =>
                    {
                        PatcherSize patcherSize;
                        if (!CheckPatcher(out patcherSize)) return false;

                        ActivateTargetWindow(Static.PATCHER_SIZES[patcherSize]);

                        return ValidatePatcher(new PatcherValidation[]
                        {
                            new PatcherValidation()
                            {
                                ExpectedResult = true,
                                PatcherRectangle = PatcherRectangle.Accept,
                                PatcherSize = patcherSize,
                                IsThreshold = false
                            }
                        });
                    },
                    () =>
                    {
                        PatcherSize patcherSize;
                        CheckPatcher(out patcherSize);
                        ClickTargetWindowRectangle(Static.PATCHER_RECTANGLES[PatcherRectangle.Accept][patcherSize]);
                    },
                    new State[]
                    {
                        State.PatcherAgreement,
                        State.Login
                    }) },
                #endregion

                #region Login State
                { State.Login, new Transition(
                    State.Login,
                    () =>
                    {
                        if (!FindWindow(Static.CLIENT_CLASS_NAME, Strings.Strings.ClientText)) return false;

                        ActivateTargetWindow(Static.CLIENT_SIZE);

                        return ValidateClient(new ClientValidation[]
                        {
                            new ClientValidation()
                            {
                                ExpectedResult = true,
                                ClientRectangle = ClientRectangle.LoginIndicator,
                                IsThreshold = false
                            },
                            new ClientValidation()
                            {
                                ExpectedResult = true,
                                ClientRectangle = ClientRectangle.LoginIndicator2,
                                IsThreshold = false
                            },
                            new ClientValidation()
                            {
                                ExpectedResult = true,
                                ClientRectangle = ClientRectangle.LoginIndicatorThreshold,
                                IsThreshold = true
                            }
                        });
                    },
                    () =>
                    {
                        Click(Static.CLIENT_CLICK_POINTS[ClientClickPoint.LoginUsername]);
                        EmptyTextBox();
                        SendText(mainForm.Config.LolId);
                        Click(Static.CLIENT_CLICK_POINTS[ClientClickPoint.LoginPassword]);
                        EmptyTextBox();
                        SendText(mainForm.Config.LolPassword);
                    },
                    new State[]
                    {
                    }) },
                #endregion
            };
        }

        public void Quit()
        {
            mainLoop = false;
        }

        public void Work()
        {
            while (mainLoop)
            {
                currentTransition = currentTransition.Work(mainForm, transitions);
            }
        }
    }
}