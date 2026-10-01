FROM node:22-slim
RUN npm install --global firebase-tools@15.32.1 && npm cache clean --force
WORKDIR /emulator
COPY firebase.json .
ENV CI=true
EXPOSE 9099
CMD ["firebase", "emulators:start", "--only", "auth", "--project", "lantern-ai-bg1709", "--non-interactive"]
