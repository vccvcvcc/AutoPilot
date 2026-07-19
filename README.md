# AutoPilot for AnyRPGCore

This folder contains a lightweight AutoPilot bootstrap for AnyRPGCore.

## How to use

1. Open the AnyRPGCore Unity project.
2. In the editor, select Tools > AutoPilot > Enable On Play.
3. Enter Play mode.
4. The bot will start in the background and write a report under AutoPilotBridge/Reports/.

## Notes

- The current adapter is intentionally minimal and uses a simple phase detector plus a looped test-play stub.
- For a real automated test loop, the adapter should be replaced with AnyRPG-specific sensors and interactions.
