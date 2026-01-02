# Clash Royale Advisor MVP (WinForms + scrcpy)

## Purpose
Clash Royale �� scrcpy �� PC �Ƀ~���[���AWinForms �A�v���ŉ�ʃL���v�`���E��͂���
���Ɂu���� / �ǂ��v�ɃJ�[�h��o���ׂ������ĕ\������ MVP ��������܂��B
��������͍s�킸�A��Ă݂̂�\�����܂��B

## System Diagram

Android (Clash Royale)
  |  USB + scrcpy
  v
scrcpy window (title: ClashRoyale)
  |  Win32 capture
  v
WinFormsApp1
  |  Motion / Elixir analysis
  v
Suggestion (dot + text)

## Setup

1) Android �[���� USB �f�o�b�O��L����
2) scrcpy ��N�� (�K�{�̃E�B���h�E�^�C�g���w��)

```
scrcpy --window-title "ClashRoyale"
```

3) �r���h / ���s

```
dotnet build
```

```
dotnet run --project src/WinFormsApp1
```

4) unsafe �L����

�{���|�W�g���ł� `AllowUnsafeBlocks` �� csproj �ɐݒ�ς݂ł��B

## Current Features

- scrcpy �E�B���h�E��L���v�`�����ĕ\��
- �t���[�������̓��̌��o
- ���w���œ�������������h�q��� (dot + text)
- �G���N�T�[�o�[�̐F����ɂ�鐄��ƕ\��

## Suggestion Logic

- MotionAnalyzer �����w�� ROI �̓��̗ʂ���E�ʂɏW�v
- ���̂�臒l�𒴂���� DefenseTrigger = true
- ElixirEstimator ���G���N�T�[�o�[�̎��F�䗦���� 0-10 �𐄒�
- SuggestionEngine ��
  - DefenseTrigger
  - Elixir >= need (default: 3)
  - 2 �t���[���A���Ńg���K�[
  - 700ms �N�[���_�E��
  �𖞂������Ƃ��ɒ�Ă�o���܂�
- ��Ĉʒu�͒�Ճ|�C���g�փX�i�b�v

## Design Policy

- ��������͈�؍s��Ȃ� (�^�b�v���M / ���͒����Ȃ�)
- ��ʏ�̒�ĕ\���̂�

## Roadmap

- [x] Phase 1: Capture + Draw MVP
- [x] Phase 2: Motion + Elixir estimation
- [x] Phase 3: Suggestion logic + tests
- [ ] Phase 4: Accuracy tuning + fixtures + parameter UI
