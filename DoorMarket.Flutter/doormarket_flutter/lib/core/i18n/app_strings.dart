import 'package:flutter/widgets.dart';

class AppStrings {
  AppStrings(this.locale);

  final Locale locale;

  bool get isFr => locale.languageCode.toLowerCase() == 'fr';

  String tr(String fr, String en) => isFr ? fr : en;

  String get splashTagline => isFr ? 'Du marche a votre porte' : 'From the Market to Your Door';

  String get welcome => isFr ? 'Bienvenue sur DoorMarket' : 'Welcome to DoorMarket';
  String get loginTitle => isFr ? 'Connexion' : 'Sign in';
  String get loginSubtitle => isFr ? 'Email / Telephone' : 'Email / Phone';
  String get passwordLabel => isFr ? 'Mot de passe' : 'Password';
  String get loginAction => isFr ? 'Se connecter' : 'Sign in';
  String get loginLoading => isFr ? 'Connexion...' : 'Signing in...';
  String get createAccount => isFr ? 'Creer un compte' : 'Create account';
  String get otpLoginDisabled => isFr ? 'OTP au login desactive.' : 'OTP for login is disabled.';

  String get registerTitle => isFr ? 'Inscription' : 'Create account';
  String get emailLabel => isFr ? 'Email' : 'Email';
  String get phoneOptionalLabel => isFr ? 'Telephone (optionnel)' : 'Phone (optional)';
  String get confirmPasswordLabel => isFr ? 'Confirmer le mot de passe' : 'Confirm password';
  String get registerAction => isFr ? 'Creer un compte' : 'Create account';
  String get registerLoading => isFr ? 'Creation...' : 'Creating...';
  String get alreadyHaveAccount => isFr ? 'Deja un compte ?' : 'Already have an account?';
  String get passwordMismatch => isFr ? 'Les mots de passe ne correspondent pas.' : 'Passwords do not match.';

  String get verifyTitle => isFr ? 'Verification' : 'Verification';
  String verifySentTo(String email) => isFr ? 'Code envoye a $email' : 'Code sent to $email';
  String verifyExpires(String minutes, String seconds) =>
      isFr ? 'Expire dans $minutes:$seconds' : 'Expires in $minutes:$seconds';
  String get otpLabel => isFr ? 'Code OTP' : 'OTP code';
  String get verifyAction => isFr ? 'Verifier' : 'Verify';
  String get verifyLoading => isFr ? 'Verification...' : 'Verifying...';
  String get resendAction => isFr ? 'Renvoyer le code' : 'Resend code';
  String get resendLoading => isFr ? 'Envoi...' : 'Sending...';

  String get languageFrench => isFr ? 'Francais' : 'French';
  String get languageEnglish => isFr ? 'English' : 'English';
  String get languageSystem => isFr ? 'Systeme' : 'System';
}
