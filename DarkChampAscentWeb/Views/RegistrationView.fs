module RegistrationView

open Falco.Markup
open Falco.Markup.Svg
open UI

let unifiedAuth (errorO: string option) =
    Elem.main [
        Attr.class' "auth-page"
        Attr.role "main"
    ] [
        Elem.div [ Attr.class' "auth-container" ] [
            // Header
            Elem.h2 [ Attr.class' "auth-header" ] [
                Text.raw "Welcome Back"
            ]
            Elem.p [ Attr.class' "auth-subtitle" ] [
                Text.raw "Sign in to continue your adventure"
            ]

            // Error message (if any)
            match errorO with
            | Some error ->
                Elem.div [ Attr.class' "error-alert" ] [
                    Text.raw error
                ]
            | None -> ()

            // Discord OAuth button
            Elem.form [
                Attr.class' "discord-form"
                Attr.methodGet
                Attr.action Route.loginDiscord
            ] [
                Elem.button [
                    Attr.type' "submit"
                    Attr.class' "btn btn-discord"
                ] [
                    Elem.svg [
                        Attr.class' "discord-icon"
                        Attr.width "24"
                        Attr.height "24"
                        Attr.viewBox "0 0 24 24"
                        Attr.fill "currentColor"
                    ] [
                        Elem.path [
                            Attr.d "M20.317 4.3671a19.8062 19.8062 0 00-4.8851-1.5152.074.074 0 00-.0784.0336c-.211.3667-.4429.8479-.6052 1.2288a18.27 18.27 0 00-5.487 0 12.64 12.64 0 00-.6144-1.2288.077.077 0 00-.0773-.0336 19.7892 19.7892 0 00-4.8850 1.5151.07.07 0 00-.0319.0287C.5239 8.5116.5239 12.0359 1.6429 15.3971a.0771.0771 0 00.0312.0424 19.9365 19.9365 0 005.8365 2.9143.0771.0771 0 00.0842-.0281c.3616-.4883.6819-1.0038.9514-1.5421a.077.077 0 00-.0042-.0856 13.1349 13.1349 0 01-1.9256-.9185.077.077 0 01-.0076-.1277c.1294-.0960.2588-.1953.3832-.2997a.076.076 0 01.0784-.0105c4.0447 1.8486 8.4234 1.8486 12.4290 0a.077.077 0 01.0787.0105c.1245 1.0447.2549 1.9416.3832 2.9997a.077.077 0 01-.0041.0856 13.107 13.107 0 01-1.9256.9185.077.077 0 00-.0042.0856c.2695.5383.5898 1.0538.9514 1.5421a.077.077 0 00.0842.028 19.963 19.963 0 005.8363-2.9143.077.077 0 00.0313-.0424c1.1162-3.3572 1.1162-6.8815-.0312-10.0489a.070.070 0 00-.0312-.0287zM8.02 15.3312c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9555-2.4189 2.157-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.9555 2.4189-2.1569 2.4189zm7.9748 0c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9554-2.4189 2.1569-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.946 2.4189-2.1568 2.4189z"
                        ] []
                    ]
                    Text.raw "Continue with Discord"
                ]
            ]

            // Separator
            Elem.div [ Attr.class' "separator" ] [
                Elem.span [] [ Text.raw "or" ]
            ]

            // Custom auth section
            Elem.div [ Attr.class' "custom-auth" ] [
                // Toggle buttons
                Elem.div [ Attr.class' "auth-toggle" ] [
                    Elem.button [
                        Attr.type' "button"
                        Attr.class' "toggle-btn active"
                        Attr.id "loginToggle"
                    ] [
                        Text.raw "Login"
                    ]
                    Elem.button [
                        Attr.type' "button"
                        Attr.class' "toggle-btn"
                        Attr.id "registerToggle"
                    ] [
                        Text.raw "Register"
                    ]
                ]

                // Login form
                Elem.form [
                    Attr.class' "auth-form"
                    Attr.id "loginForm"
                    Attr.method "post"
                    Attr.action Route.loginCustom
                ] [
                    Elem.div [ Attr.class' "form-group" ] [
                        Elem.label [
                            Attr.for' "login-nickname"
                            Attr.class' "form-label"
                        ] [
                            Text.raw "Nickname"
                        ]
                        Elem.input [
                            Attr.type' "text"
                            Attr.id "login-nickname"
                            Attr.name "nickname"
                            Attr.class' "form-input"
                            Attr.placeholder "Enter nickname"
                            Attr.required
                        ]
                    ]

                    Elem.div [ Attr.class' "form-group" ] [
                        Elem.label [
                            Attr.for' "login-password"
                            Attr.class' "form-label"
                        ] [
                            Text.raw "Password"
                        ]
                        Elem.input [
                            Attr.type' "password"
                            Attr.id "login-password"
                            Attr.name "password"
                            Attr.class' "form-input"
                            Attr.placeholder "Enter password"
                            Attr.required
                        ]
                    ]

                    Elem.div [ Attr.class' "form-actions" ] [
                        Elem.button [
                            Attr.type' "submit"
                            Attr.class' "btn btn-primary"
                        ] [
                            Text.raw "Login"
                        ]
                    ]
                ]

                // Registration form (hidden by default)
                Elem.form [
                    Attr.class' "auth-form hidden"
                    Attr.id "registerForm"
                    Attr.method "post"
                    Attr.action Route.reg
                    Attr.novalidate
                ] [
                    Elem.div [ Attr.class' "form-group" ] [
                        Elem.label [
                            Attr.for' "register-nickname"
                            Attr.class' "form-label"
                        ] [
                            Text.raw "Nickname"
                        ]
                        Elem.input [
                            Attr.type' "text"
                            Attr.id "register-nickname"
                            Attr.name "nickname"
                            Attr.class' "form-input"
                            Attr.placeholder "Enter nickname"
                            Attr.minlength "3"
                            Attr.maxlength "16"
                            Attr.required
                        ]
                        Elem.div [
                            Attr.class' "validation-message"
                            Attr.id "nickname-error"
                        ] [
                            Text.raw "Nickname must be 3-16 characters"
                        ]
                    ]

                    Elem.div [ Attr.class' "form-group" ] [
                        Elem.label [
                            Attr.for' "register-password"
                            Attr.class' "form-label"
                        ] [
                            Text.raw "Password"
                        ]
                        Elem.input [
                            Attr.type' "password"
                            Attr.id "register-password"
                            Attr.name "password"
                            Attr.class' "form-input"
                            Attr.placeholder "Enter password"
                            Attr.minlength "8"
                            Attr.maxlength "24"
                            Attr.required
                        ]
                        Elem.div [
                            Attr.class' "validation-message"
                            Attr.id "password-error"
                        ] [
                            Text.raw "Password must be 8-24 characters"
                        ]
                    ]

                    Elem.div [ Attr.class' "form-group" ] [
                        Elem.label [
                            Attr.for' "password-confirm"
                            Attr.class' "form-label"
                        ] [
                            Text.raw "Confirm Password"
                        ]
                        Elem.input [
                            Attr.type' "password"
                            Attr.id "password-confirm"
                            Attr.name "password-confirm"
                            Attr.class' "form-input"
                            Attr.placeholder "Confirm password"
                            Attr.required
                        ]
                        Elem.div [
                            Attr.class' "validation-message"
                            Attr.id "password-confirm-error"
                        ] [
                            Text.raw "Passwords do not match"
                        ]
                    ]

                    Elem.div [ Attr.class' "form-actions" ] [
                        Elem.button [
                            Attr.type' "submit"
                            Attr.class' "btn btn-primary"
                            Attr.id "registerButton"
                        ] [
                            Text.raw "Register"
                        ]
                    ]
                ]
            ]
        ]

        // JavaScript for toggle and validation
        Elem.script [] [
            Text.raw """
            (function() {
                const loginToggle = document.getElementById('loginToggle');
                const registerToggle = document.getElementById('registerToggle');
                const loginForm = document.getElementById('loginForm');
                const registerForm = document.getElementById('registerForm');

                // Toggle between login and registration
                loginToggle.addEventListener('click', function() {
                    loginToggle.classList.add('active');
                    registerToggle.classList.remove('active');
                    loginForm.classList.remove('hidden');
                    registerForm.classList.add('hidden');
                });

                registerToggle.addEventListener('click', function() {
                    registerToggle.classList.add('active');
                    loginToggle.classList.remove('active');
                    registerForm.classList.remove('hidden');
                    loginForm.classList.add('hidden');
                });

                // Registration form validation
                const nicknameInput = document.getElementById('register-nickname');
                const passwordInput = document.getElementById('register-password');
                const passwordConfirmInput = document.getElementById('password-confirm');
                const submitButton = document.getElementById('registerButton');
                
                const nicknameError = document.getElementById('nickname-error');
                const passwordError = document.getElementById('password-error');
                const passwordConfirmError = document.getElementById('password-confirm-error');

                function validateNickname() {
                    const value = nicknameInput.value.trim();
                    if (value.length < 3 || value.length > 16) {
                        nicknameInput.setAttribute('aria-invalid', 'true');
                        nicknameError.classList.add('show');
                        return false;
                    } else {
                        nicknameInput.setAttribute('aria-invalid', 'false');
                        nicknameError.classList.remove('show');
                        return true;
                    }
                }

                function validatePassword() {
                    const value = passwordInput.value;
                    if (value.length < 8 || value.length > 24) {
                        passwordInput.setAttribute('aria-invalid', 'true');
                        passwordError.classList.add('show');
                        return false;
                    } else {
                        passwordInput.setAttribute('aria-invalid', 'false');
                        passwordError.classList.remove('show');
                        validatePasswordConfirm();
                        return true;
                    }
                }

                function validatePasswordConfirm() {
                    const password = passwordInput.value;
                    const confirm = passwordConfirmInput.value;
                    
                    if (confirm.length === 0) {
                        passwordConfirmInput.setAttribute('aria-invalid', 'true');
                        passwordConfirmError.classList.add('show');
                        return false;
                    }
                    
                    if (password !== confirm) {
                        passwordConfirmInput.setAttribute('aria-invalid', 'true');
                        passwordConfirmError.classList.add('show');
                        return false;
                    } else {
                        passwordConfirmInput.setAttribute('aria-invalid', 'false');
                        passwordConfirmError.classList.remove('show');
                        return true;
                    }
                }

                function updateButtonState() {
                    const nickname = nicknameInput.value.trim();
                    const isNicknameValid = nickname.length >= 3 && nickname.length <= 16;
                    const password = passwordInput.value;
                    const isPasswordValid = password.length >= 8 && password.length <= 24;
                    const isPasswordConfirmValid = passwordConfirmInput.value.length > 0 && passwordInput.value === passwordConfirmInput.value;
                    
                    const allValid = isNicknameValid && isPasswordValid && isPasswordConfirmValid;
                    submitButton.disabled = !allValid;
                }

                nicknameInput.addEventListener('blur', validateNickname);
                nicknameInput.addEventListener('input', () => {
                    validateNickname();
                    updateButtonState();
                });

                passwordInput.addEventListener('blur', validatePassword);
                passwordInput.addEventListener('input', () => {
                    validatePassword();
                    updateButtonState();
                });

                passwordConfirmInput.addEventListener('blur', validatePasswordConfirm);
                passwordConfirmInput.addEventListener('input', () => {
                    validatePasswordConfirm();
                    updateButtonState();
                });

                registerForm.addEventListener('submit', function(e) {
                    const isNicknameValid = validateNickname();
                    const isPasswordValid = validatePassword();
                    const isPasswordConfirmValid = validatePasswordConfirm();

                    if (!isNicknameValid || !isPasswordValid || !isPasswordConfirmValid) {
                        e.preventDefault();
                    }
                });

                updateButtonState();
            })();
            """
        ]
    ]