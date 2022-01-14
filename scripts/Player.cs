using Godot;
using System;

public class Player : Spatial
{
    private Globals G;
    private Statistics S;
    private Level L;
    private Interface I;

    private Random rnd = new Random();

    private Tween playerTween;
    private MeshInstance playerMesh;
    private Camera playerCamera;

    private AnimationPlayer cameraBounce;
    private AnimationPlayer cameraAnimation;

    private bool canMove = false;
    private bool playerDead = false;
    private bool cameraRotating = false;

    private float lifeLossRate = 0.04f;
    private float lifeGainRate = 2.0f;
    private float animSpeed = 0.25f;

    private int speedupCounter = 0;
    private int nextCycle = 0;
    private int maxCycle = 20;
    private int frames = 0;
    private int framesMode = 0;
    private int faliedPM = 0;

    private int addScoreBy = 1;

    String[] keys = {"ui_up", "ui_right", "ui_down", "ui_left"};

    // Init function
    public override void _Ready()
    {
        G = GetNode<Globals>("/root/Globals");
        S = GetNode<Statistics>("/root/Level/Interface/Main/StatsButton");
        L = GetNode<Level>("/root/Level");
        I = GetNode<Interface>("/root/Level/Interface");

        playerTween = GetNode<Tween>("Tween");
        playerMesh = this.GetNode<MeshInstance>("Spatial/Mesh");
        playerCamera = this.GetNode<Camera>("Camera");

        cameraAnimation = playerCamera.GetNode<AnimationPlayer>("CameraPan");
        cameraBounce = GetNode<AnimationPlayer>("CameraBounce");

        G.ResetVars();
        G.playerPosition = this.Translation;

        nextCycle = G.GetMaxCycle(maxCycle, 4);
    }

    // Debug for now
    public override void _Process(float delta)
    {
        if (Input.IsActionPressed(keys[0]) &&
                Input.IsActionPressed(keys[1]))
            if (canMove) CheckMove(0);

        if (Input.IsActionPressed(keys[1]) &&
                Input.IsActionPressed(keys[2]))
            if (canMove) CheckMove(1);

        if (Input.IsActionPressed(keys[2]) &&
                Input.IsActionPressed(keys[3]))
            if (canMove) CheckMove(2);

        if (Input.IsActionPressed(keys[3]) &&
                Input.IsActionPressed(keys[0]))
            if (canMove) CheckMove(3);
    }

    // Runs every game tick
    public override void _PhysicsProcess(float delta)
    {
        // No life game_over check
        if ((G.playerHealth <= 0) && !playerDead)
            GameOver();

        FramesCalculation();

        if (!G.perspectiveMode && (framesMode % 2) == 0)
            LooseHealth();
    }

    public void CheckMove(int dir)
    {
        if (!canMove) return;

        // Calculation based on camera rotation
        G.animDirection = G.RetranslateDirection(dir);
        
        if (G.IsMoveLegal())
        {
            CorrectScoreCalculation();
            return;
        }

        RebounceCheck(dir);
    }

    private void LooseHealth()
    {
        if (G.firstMove) return;

        // Take less life when rotating
        if (cameraRotating) G.playerHealth -= lifeLossRate / 4;
        else G.playerHealth -= lifeLossRate / 2;

        I.CalculateHealthBar();
    }

    private void FramesCalculation()
    {
        frames++;
        if (frames >= 120)
        {
            frames = 0;
            L.CreateDecorations();
        }

        // Disable after 5s
        if (!G.perspectiveMode) return;

        framesMode++;

        if ((framesMode % 2) == 0)
        {
            I.CalculatePerspectiveBar(framesMode);
        }

        if (framesMode >= 600)
        {
            framesMode = 0;
            EnablePerspectiveMode(false);
        }
    }

    private void CorrectScoreCalculation()
    {
        EnableControls(false, false);

        Vector3 oldPos = this.Translation;
        Vector3 newPos = G.DirectionCalc();

        // Movement animation
        PlayTweenAnim("translation", oldPos, newPos, animSpeed);
        
        G.sessionScore += addScoreBy;
        I.AddScore();
        
        // Don't active it twice by a small chance
        if (!G.perspectiveMode) RollPerspectiveMode();

        // Progress the game
        L.GeneratePlatform();
        GiveHealth(lifeGainRate);
        DifficultyIncrease();
    }

    private void RollPerspectiveMode()
    {
        // 1% chance to activate special mode after 20 moves
        int side = rnd.Next(100);
        if (side < 1 && faliedPM >= 20)
        {
            EnablePerspectiveMode(true);
            faliedPM = 0;
            return;
        }

        faliedPM++;

        // Quadruple the chances after unlucky 100 moves
        if (side < 4 && faliedPM >= 100)
        {
            EnablePerspectiveMode(true);
            faliedPM = 0;
        }
    }

    private void DifficultyIncrease()
    {
        speedupCounter++;
        if (speedupCounter < nextCycle) return;

        lifeLossRate += 0.01f;
        lifeGainRate += 0.25f;

        // Cap player speed at 0.15f
        if (animSpeed > 0.15f) animSpeed -= 0.005f;

        speedupCounter = 0;

        CheckAndGenerateNewCycle();
        RotateCameraBy(G.DetermineRotationAmmount());
    }

    private void CheckAndGenerateNewCycle()
    {
        maxCycle--;

        if (maxCycle < 5)
        {
            maxCycle = 5;
            nextCycle = G.GetMaxCycle(maxCycle, 4);
            return;
        }

        nextCycle = G.GetMaxCycle(maxCycle, 5);
    }

    private void RotateCameraBy(int ammount)
    {
        // Get random rotation direction
        bool clockwise = G.RandomBool();

        // Camera rotation section
        if (clockwise) G.camRotIndex += ammount;
        else G.camRotIndex -= ammount;

        // camRotIndex = 3 -> DEFAULT
        if (G.camRotIndex > 3) G.camRotIndex -= 4;
        if (G.camRotIndex < 0) G.camRotIndex += 4;

        // Disable controls for animation duration
        EnableControls(false, false);
        PlayCorrectAnimation(clockwise, ammount);
    }

    private void PlayCorrectAnimation(bool clockwise, int rotations)
    {
        int rotateBy = 90 * rotations;
        // More rotations take longer
        float time = rotations * animSpeed * 2;

        Vector3 oldRotRad = this.RotationDegrees;
        Vector3 newRot = oldRotRad;

        if (clockwise) newRot.y += rotateBy;
        else newRot.y -= rotateBy;

        PlayTweenAnim("rotation_degrees", oldRotRad, newRot, time);
    }

    // Player movement and camera rotations
    public void PlayTweenAnim(String type, Vector3 oldV, Vector3 newV, float time)
    {
        Tween.TransitionType trans = Tween.TransitionType.Quad;
        Tween.EaseType ease = Tween.EaseType.InOut;

        playerTween.InterpolateProperty(this, type, oldV, newV, time, trans, ease);
        playerTween.Start();
    }
	
    // Reenable controls
    private void _on_CameraRotation_animation_finished(String anim_name)
    {
        EnableControls(true, false);
        cameraRotating = false;
    }

    private void _on_Tween_tween_all_completed()
    {
        // Update global position at the end of animation
        G.playerPosition = this.Translation;

        // Small bug fix
        if (!cameraRotating) EnableControls(true, false);
    }

    private void GiveHealth(float ammount)
    {
        // Health cap check
        if ((G.playerHealth + ammount) > G.fullHealth)
        {
            G.playerHealth = G.fullHealth;
            return;
        }

        G.playerHealth += ammount;
    }
        
    private void RebounceCheck(int original_dir)
    {
        // No penalties during perspective mode
        if (!G.perspectiveMode)
        {
            EnableControls(false, true);

            // Wrong move penalty
            if (!G.firstMove) G.playerHealth -= 10;

            // Instant game over
            if ((G.playerHealth <= 0) && !playerDead)
            {
                GameOver();
                return;
            }
        }

        else EnableControls(false, false);

        // Animate player rebounce
        cameraBounce.Play("Bounce" + original_dir.ToString());
    }

    private void GameOver()
    {
        // Preventing movement after death
        playerDead = true;
        EnableControls(false, true);

        if (G.sessionScore > G.highScore)
        {
            G.highScore = G.sessionScore;

            // Give more time for the new highscore animation
            cameraAnimation.Play("CameraUpLong");
            I.HideUiAnimations(true);
        }

        else
        {
            // Outro animations
            cameraAnimation.Play("CameraUp");
            I.HideUiAnimations(false);
        }

        S.UploadStatistics();
    }

    private void EnableControls(bool enable, bool red)
    {
        if (enable)
        {
            canMove = true;

            if (G.perspectiveMode) return;
            ChangePlayerColor(false);

            return;
        }

        canMove = false;
        if (red) ChangePlayerColor(true);
    }

    private void EnablePerspectiveMode(bool perspective)
    {
        G.perspectiveMode = perspective;
        I.PlayBlindAnim(perspective);

        if (perspective)
        {
            // Replenish full health
            GiveHealth(G.fullHealth);
            I.CalculateHealthBar();

            ChangePlayerColor(true);
            I.ColorHealthbarRed(true);

            // Double score when in perspective mode
            addScoreBy = 2;
            return;
        }

        addScoreBy = 1;

        ChangePlayerColor(false);
        I.ColorHealthbarRed(false);
    }

    private void ChangePlayerColor(bool red)
    {
        if (red)
        {
            if (playerMesh.GetSurfaceMaterial(0) == G.emissionRed) return;
            playerMesh.SetSurfaceMaterial(0, G.emissionRed);
            return;
        }

        if (playerMesh.GetSurfaceMaterial(0) == G.emissionBlue) return;
        playerMesh.SetSurfaceMaterial(0, G.emissionBlue);
    }
        
    private void _on_CameraPan_animation_finished(String anim_name)
    {
        if (anim_name == "CameraUp" || anim_name == "CameraUpLong")
        {
            GetTree().ReloadCurrentScene();
            return;
        }
            
        EnableControls(true, false);
    }
}