# Public-Speaking-Help

Public-Speaking-Help is a two-part prototype for practicing speeches in a more realistic setting:

- A Unity VR application, built as `Virtual Stage VR`, that places the speaker inside rehearsal environments such as a theater or classroom.
- A lightweight web companion, `StageNotes`, for editing and organizing flashcard sets that support speech prep.

The project objective is to help a speaker rehearse with environmental pressure, cue cards, and adjustable room conditions instead of practicing only from static notes or a flat screen.

## Project Overview

At a high level, this repository combines immersive rehearsal and note preparation:

- The Unity project provides the practice space, scene switching, audience controls, flashcard interaction, and headset-oriented input.
- The web companion provides a browser-based flashcard editor plus a simple local JSON-backed API.

The intended workflow appears to be:

1. Create or edit speaking notes as flashcard sets in the web companion.
2. Launch the VR app and choose a practice environment such as `Theater` or `Classroom`.
3. Rehearse while using flashcards as prompts and adjusting crowd density or crowd volume to simulate different speaking conditions.

## Current State

The repository already contains the core building blocks for that flow, but it is still a prototype:

- The Unity app includes active rehearsal scenes, flashcard display logic, audience settings, scene switching, and microphone-level sampling.
- The web companion serves a local editing UI and includes JSON-backed set endpoints, but its create/update workflow is still partly scaffolded.
- Direct data sync between the web app and Unity is not finished yet. The Unity `SetList` script still seeds a local demo set, and `GetSetData()` is a placeholder for future server integration.
- The repository also includes an Android build artifact at `VR Public Speaking Project/Build/VRPublicSpeaking.apk`.

## Repository Structure

### `VR Public Speaking Project/`

Unity 6 XR project (`6000.0.47f1`) for the immersive training experience.

- `Assets/Scenes/`
  Main scenes for the user experience:
  `Theater.unity`, `Classroom.unity`, `MicrophoneTest.unity`, and the default `SampleScene.unity`.
- `Assets/Scripts/FlashCard.cs`
  Handles flashcard navigation, controller input bindings, and card respawn behavior if a card leaves the interaction zone.
- `Assets/Settings.cs`
  Adjusts audience density and crowd audio volume through UI sliders.
- `Assets/SceneManager.cs`
  Loads practice scenes from a dropdown menu.
- `Assets/SetList.cs`
  Stores available flashcard sets and populates the set-selection UI. This currently uses a hardcoded demo set and contains a TODO for HTTP loading.
- `Assets/SetButton.cs`
  Connects a selected set to the preview panel.
- `Assets/FlashCardPreview.cs`
  Builds a preview of each card in a set and pushes the selected set into the active flashcard system.
- `Assets/CardFlip.cs`
  Animates card flipping between front and back text.
- `Assets/MicrophoneInputManager.cs`
  Starts microphone capture, plays the live clip through an `AudioSource`, and logs RMS volume data.
- `Assets/Gwangju_3D asset/`, `Assets/school/`, `Assets/DenysAlmaral/`, `Assets/VRTemplateAssets/`, `Assets/Samples/`
  Imported environments, characters, XR starter assets, and supporting art/content used to build the rehearsal spaces.
- `Packages/manifest.json`
  Declares major Unity dependencies, including OpenXR, XR Interaction Toolkit, XR Hands, URP, the Input System, and UGUI.
- `ProjectSettings/EditorBuildSettings.asset`
  Shows `Theater` and `Classroom` as the enabled main scenes.
- `Build/VRPublicSpeaking.apk`
  Existing Android build output.

### `Web Companion Server/`

Node/Express companion app for editing flashcard sets and exposing local set APIs.

- `server.js`
  Serves the front end and exposes `/api/sets` endpoints backed by a JSON file.
- `public/index.html`
  Main `StageNotes` interface for selecting sets and editing front/back card content.
- `public/script.js`
  Front-end logic for navigating cards, tracking the active set, and wiring the editor toward the local API.
- `public/styles.css`
  Styling for the editor layout and set list.
- `data/db.json`
  Local persistence for saved sets.
- `package.json`
  Express-based server dependencies.

## Core Features

- VR practice environments for different speaking contexts.
- Scene switching between at least a theater and classroom.
- Adjustable audience density to simulate crowd size.
- Adjustable crowd audio volume to simulate room pressure/noise.
- Flashcard set selection, preview, flipping, and in-session navigation.
- Microphone input capture for basic live speech monitoring.
- Local browser-based flashcard editing through the companion app.

## Tech Stack

- Unity 6
- OpenXR
- XR Interaction Toolkit
- XR Hands
- Unity Input System
- Universal Render Pipeline
- Node.js
- Express
- Plain HTML, CSS, and JavaScript
- JSON file storage

## How The Pieces Fit Together

This project is best understood as a rehearsal system with two interfaces:

- Unity is the presentation and interaction layer for practicing a talk in simulated spaces.
- The web companion is the preparation layer for creating the note cards the speaker is expected to use during rehearsal.

The long-term value of the project is the combination of both: preparation on desktop, then rehearsal in VR.

## Notes

- `Library/`, `Logs/`, `UserSettings/`, and `node_modules/` are present in the repository and are generated/runtime-heavy project folders rather than hand-authored application logic.
- The Unity and web companion pieces are conceptually connected, but the API-to-Unity flashcard sync is not fully implemented yet.
