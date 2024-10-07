vpk download s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret PTvDYyN5UcKYDK
vpk pack -u VesperApp -v 1.0.1.23 -p publish
vpk upload s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret PTvDYyN5UcKYDK