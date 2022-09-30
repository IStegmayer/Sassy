using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game._Scripts.Sassy
{
    public class SassyController : MonoBehaviour
    {
        [SerializeField] private int _speed;
        [SerializeField] private float _maxLaunchSpeed;
        [SerializeField] private float _maxChargeTime;
        [SerializeField] private float _ricoBoost;

        private float _turnSmoothTime = 0.05f;
        private float turnSmoothVelocity;
        private Vector2 _movementInput = Vector2.zero;
        private Rigidbody _rigidbody;
        private Animator _animator;
        private float _startChargeTime = 0;
        private float _chargeTime = 0;
        private float _scale = Constants.BASE_SCALE;
        private bool _keyDown = false;
        private bool _keyUp = false;
        private bool _windslashDown = false;
        private int _id;

        private enum PlayerStates
        {
            Launching,
            Charging,
            Slashing,
            Walking,
            Idle
        }

        private PlayerStates _playerState = PlayerStates.Idle;

        private enum AnimProperties
        {
            IsIdle = 0,
            IsCharging = 1,
            IsLaunching = 2,
            IsSlashing = 3
        }

        private AnimProperties _animState = AnimProperties.IsIdle;

        private static class Constants
        {
            public const float BASE_SCALE = 0.17f;
        }

        void Start()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            SetPlayerState(PlayerStates.Idle, AnimProperties.IsIdle);
        }

        void Update()
        {
        }

        private void FixedUpdate()
        {
            if (GameManager.Instance.State != GameState.Playing) return;
            if (_windslashDown)
            {
                WindSlash();
                _windslashDown = false;
            }

            if (_keyDown)
            {
                _startChargeTime = Time.time;
                SetPlayerState(PlayerStates.Charging, AnimProperties.IsCharging);
                _rigidbody.velocity = Vector2.zero;
                _keyDown = false;
            }

            if (_keyUp && _playerState == PlayerStates.Charging)
            {
                _chargeTime = Time.time - _startChargeTime;
                //max launch 3 sec
                if (_chargeTime > _maxChargeTime)
                {
                    _chargeTime = _maxChargeTime;
                }

                float launchSpeed = (_chargeTime / _maxChargeTime) * _maxLaunchSpeed;
                _rigidbody.velocity +=
                    new Vector3(_movementInput.x * launchSpeed, 0.0f, _movementInput.y * launchSpeed);
                SetPlayerState(PlayerStates.Launching, AnimProperties.IsLaunching);
                _keyUp = false;
            }

            if (_playerState == PlayerStates.Charging)
            {
                if (0.07 < _scale)
                {
                    _scale = Constants.BASE_SCALE - ((Time.time - _startChargeTime) / _maxChargeTime);
                    transform.localScale = new Vector3(_scale, _scale, _scale);
                }
            }

            if (_playerState == PlayerStates.Launching || _playerState == PlayerStates.Slashing)
            {
                Launch();
            }
            else if (_playerState == PlayerStates.Idle)
            {
                _rigidbody.velocity = Vector3.zero;
                if (_movementInput.magnitude >= 0.1f)
                {
                    // we pass x first so we get a clockwise rotation
                    float targetAngle = Mathf.Atan2(_movementInput.x, _movementInput.y) * Mathf.Rad2Deg;
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity,
                        _turnSmoothTime);
                    transform.rotation = Quaternion.Euler(0f, angle, 0f);
                }
                
                Vector3 position = _rigidbody.position;
                position.x += _movementInput.x * _speed * Time.deltaTime;
                position.z += _movementInput.y * _speed * Time.deltaTime;
                _rigidbody.MovePosition(position);

            }
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            _movementInput = context.ReadValue<Vector2>();
        }

        public void OnWindslash(InputAction.CallbackContext context)
        {
            if (_playerState == PlayerStates.Launching && context.action.WasPressedThisFrame())
                _windslashDown = true;
        }

        public void OnLaunch(InputAction.CallbackContext context)
        {
            if (context.action.WasPressedThisFrame() &&
                (_playerState == PlayerStates.Walking || _playerState == PlayerStates.Idle))
                _keyDown = true;
            if (context.action.WasReleasedThisFrame() && _playerState == PlayerStates.Charging)
                _keyUp = true;
        }

        void Launch()
        {
            if (_rigidbody.velocity.magnitude < _speed * 2)
            {
                _startChargeTime = 0.0f;
                _chargeTime = 0.0f;
                _scale = Constants.BASE_SCALE;
                transform.localScale = new Vector3(_scale, _scale, _scale);
                SetPlayerState(PlayerStates.Idle, AnimProperties.IsIdle);
            }
        }

        void WindSlash()
        {
            AudioSystem.Instance.PlaySound("windSlash", transform.position);
            SetPlayerState(PlayerStates.Slashing, AnimProperties.IsSlashing);
            transform.localScale = new Vector3(Constants.BASE_SCALE, Constants.BASE_SCALE, Constants.BASE_SCALE);
        }

        public void EndWindSlash()
        {
            SetPlayerState(PlayerStates.Launching, AnimProperties.IsLaunching);
        }

        public void ResetLaunch()
        {
            SetPlayerState(PlayerStates.Launching, AnimProperties.IsSlashing);
            transform.localScale = new Vector3(_scale, _scale, _scale);
            Invoke(nameof(EndWindSlash), 0.2f);
        }

        void SetPlayerState(PlayerStates newPlayerState, AnimProperties animProperty)
        {
            _animator.SetInteger("CurrentState", (int) animProperty);
            _playerState = newPlayerState;
            _animState = animProperty;
        }
    }
}